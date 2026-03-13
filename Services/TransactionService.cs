using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using ZkbioDashboard.Constants;
using ZkbioDashboard.Helpers;
using ZkbioDashboard.Models;

namespace ZkbioDashboard.Services;

public interface ITransactionService
{
    Task<(IEnumerable<AccTransaction> Data, int TotalCount)> GetTransactionsPagedAsync(int pageIndex, int pageSize, TransactionFilter filter);
    Task<IEnumerable<AccTransaction>> GetTransactionsForExportAsync(TransactionFilter filter);
    Task<IEnumerable<AttendanceRecord>> GetAttendanceReportAsync(DateTime date, string? pin = null, string? factory = null);
    Task<IEnumerable<AttendanceRecord>> GetContractorsReportAsync(DateTime date, string? pin = null, string? factory = null);
    Task<IEnumerable<AttendanceRecord>> GetPersonalAttendanceReportAsync(string pin, DateTime fromDate, DateTime toDate);
    Task<IEnumerable<AccTransaction>> GetTransactionsByRangeAsync(string pin, DateTime start, DateTime end);

    /// <summary>
    /// Returns employees whose first gate-out occurred within <paramref name="thresholdMinutes"/> of their Attend In.
    /// Used to detect "clocked-in-then-ran-to-parking" behaviour.
    /// </summary>
    Task<IEnumerable<EarlyExitRecord>> GetEarlyExitReportAsync(DateTime date, int thresholdMinutes = 1, string? factory = null);
}

public class TransactionService : ITransactionService
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan AllTransactionsCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DepartmentCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly string[] ExcludedPinTokens = ["KH", "TT", "SU"];

    private sealed record DepartmentCacheEntry(
        List<Department> Departments,
        Dictionary<string, Department> ById,
        Dictionary<string, string> ByCode,
        Dictionary<string, string> ByName);

    public TransactionService(IConfiguration configuration, IMemoryCache cache)
    {
        _configuration = configuration;
        _cache = cache;
    }

    private IEnumerable<string> GetConnectionStrings() =>
        _configuration.GetSection("ConnectionStrings").GetChildren().Select(x => x.Value!);

    // -------------------------------------------------------------------------
    // All Transactions (paged + export)
    // -------------------------------------------------------------------------

    public async Task<(IEnumerable<AccTransaction> Data, int TotalCount)> GetTransactionsPagedAsync(
        int pageIndex, int pageSize, TransactionFilter filter)
    {
        string cacheKey = BuildAllTransactionsCacheKey(pageIndex, pageSize, filter);
        if (_cache.TryGetValue<(IReadOnlyList<AccTransaction> Data, int TotalCount)>(cacheKey, out var cachedPage))
            return (cachedPage.Data, cachedPage.TotalCount);

        var (whereSql, parameters) = TransactionQueryHelper.BuildWhereClause(filter);

        string countSql = "SELECT COUNT(*) FROM acc_transaction" + whereSql;
        string dataSql  = TransactionQueryHelper.TransactionSelectSql + whereSql + " ORDER BY event_time DESC";

        var tasks = GetConnectionStrings().Select(async cs =>
        {
            using IDbConnection db = new SqlConnection(cs);
            int    count = await db.ExecuteScalarAsync<int>(countSql, parameters);
            var    rows  = await db.QueryAsync<AccTransaction>(dataSql, parameters);
            return (rows, count);
        });

        var results = await Task.WhenAll(tasks);

        var allData = results
            .SelectMany(r => r.rows)
            .Where(t => !HasExcludedPinToken(t.Pin))
            .OrderByDescending(t => t.EventTime)
            .ToList();
        int totalCount = allData.Count;

        var pagedData = allData.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
        _cache.Set(cacheKey, (pagedData.AsReadOnly(), totalCount), AllTransactionsCacheDuration);
        return (pagedData, totalCount);
    }

    public async Task<IEnumerable<AccTransaction>> GetTransactionsForExportAsync(TransactionFilter filter)
    {
        var (whereSql, parameters) = TransactionQueryHelper.BuildWhereClause(filter);
        string dataSql = TransactionQueryHelper.TransactionSelectSql + whereSql + " ORDER BY event_time DESC";

        var tasks = GetConnectionStrings().Select(async cs =>
        {
            using IDbConnection db = new SqlConnection(cs);
            return await db.QueryAsync<AccTransaction>(dataSql, parameters);
        });

        var results = await Task.WhenAll(tasks);
        return results
            .SelectMany(r => r)
            .Where(t => !HasExcludedPinToken(t.Pin))
            .OrderByDescending(t => t.EventTime);
    }

    // -------------------------------------------------------------------------
    // Attendance Report (Exception Times)
    // -------------------------------------------------------------------------

    public async Task<IEnumerable<AttendanceRecord>> GetAttendanceReportAsync(
        DateTime date, string? pin = null, string? factory = null)
    {
        // Report window: 04:00 to catch early Ca 4 (05:30) through next 04:00
        var reportStart = date.Date.AddHours(4);
        var reportEnd   = reportStart.AddDays(1);
        var queryStart  = reportStart.AddHours(-12);
        var queryEnd    = reportStart.AddHours(36);

        // ── Phase 1: collect raw data from every server ──────────────────────
        // Employees can touch gate devices on one server and ATT devices on
        // another. We must merge all transactions per PIN across servers before
        // building attendance records; otherwise gate punches are silently lost.

        // allTransactions: every raw transaction across all servers
        var allTransactions = new List<dynamic>();

        // perServerMeta: metadata keyed by the PIN's "home server" (the server
        //   where the ATT/dept transaction was found for that PIN).
        //   value = (deptDict, rootNames, deptMapByCode, deptMapByName, factoryCluster)
        var pinServerMeta = new Dictionary<string, (
            Dictionary<string, Department> DeptDict,
            IEnumerable<string> RootNames,
            Dictionary<string, string> DeptMapByCode,
            Dictionary<string, string> DeptMapByName,
            string FactoryCluster)>(StringComparer.OrdinalIgnoreCase);

        // pinAllowedSet: PINs that have at least one transaction in an allowed dept
        var pinAllowedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var connStr in GetConnectionStrings())
        {
            using var connection = new SqlConnection(connStr);

            var departmentCache = await GetDepartmentCacheEntryAsync(connStr, connection);
            var departments     = departmentCache.Departments;
            var deptDict        = departmentCache.ById;
            var serverIp        = DepartmentHelper.GetServerIp(connStr);
            var factoryCluster  = ResolveFactoryClusterByServerIp(serverIp);
            var rootNames       = ServerConstants.GetAttendanceRootNames(serverIp);
            var allowedDepts    = DepartmentHelper.BuildAllowedDepartments(departments, rootNames);
            var deptMapByCode   = departmentCache.ByCode;
            var deptMapByName   = departmentCache.ByName;

            if (!allowedDepts.Any())
                continue;

            var transactions = await FetchTransactions(connection, queryStart, queryEnd, pin);
            if (!transactions.Any())
                continue;

            // Accumulate all transactions (gate + attendance) for later cross-server merge
            allTransactions.AddRange(transactions);

            // Identify PINs that belong to this server's allowed departments and
            // record the server metadata for building their attendance records.
            foreach (var t in transactions)
            {
                var pinVal = (string)t.PIN;
                if (pinAllowedSet.Contains(pinVal))
                    continue;   // already confirmed as valid from another server

                var deptId = DepartmentHelper.ResolveDeptId(
                    t.DeptCode != null ? (string)t.DeptCode : null,
                    t.DeptName != null ? (string)t.DeptName : null,
                    deptMapByCode, deptMapByName, departments);

                if (deptId != null && allowedDepts.Contains(deptId))
                {
                    pinAllowedSet.Add(pinVal);
                    if (!pinServerMeta.ContainsKey(pinVal))
                        pinServerMeta[pinVal] = (deptDict, rootNames, deptMapByCode, deptMapByName, factoryCluster);
                }
            }
        }

        // ── Phase 2: build one record per PIN from the merged dataset ─────────
        var allRecords = allTransactions
            .Where(t => pinAllowedSet.Contains((string)t.PIN))
            .GroupBy(t => (string)t.PIN)
            .Select(g =>
            {
                var pinVal = g.Key;
                var meta   = pinServerMeta[pinVal];
                return BuildAttendanceRecord(
                    pinVal, date.Date,
                    g.OrderBy(t => (DateTime)t.EventTime).ToList(),
                    reportStart, reportEnd,
                    meta.DeptDict, meta.RootNames,
                    meta.DeptMapByCode, meta.DeptMapByName,
                    meta.FactoryCluster);
            })
            .Where(r => r != null && (string.IsNullOrEmpty(factory) || r!.Factory == factory))
            .ToList();

        return allRecords
            .Where(r => r != null)
            .OrderBy(r => r!.DeptName).ThenBy(r => r!.FullName)!;
    }

    // -------------------------------------------------------------------------
    // Personal Attendance Report (date range for a single employee)
    // -------------------------------------------------------------------------

    public async Task<IEnumerable<AttendanceRecord>> GetPersonalAttendanceReportAsync(
        string pin, DateTime fromDate, DateTime toDate)
    {
        fromDate = fromDate.Date;
        toDate   = toDate.Date;

        var allRecords = new List<AttendanceRecord>();

        foreach (var connStr in GetConnectionStrings())
        {
            using var connection = new SqlConnection(connStr);

            var departmentCache = await GetDepartmentCacheEntryAsync(connStr, connection);
            var departments  = departmentCache.Departments;
            var deptDict     = departmentCache.ById;
            var serverIp     = DepartmentHelper.GetServerIp(connStr);
            var factoryCluster = ResolveFactoryClusterByServerIp(serverIp);
            var rootNames    = ServerConstants.GetPersonalRootNames(serverIp);
            var allowedDepts = DepartmentHelper.BuildAllowedDepartments(departments, rootNames);

            var queryStart = fromDate.AddHours(-12);
            var queryEnd   = toDate.AddDays(1).AddHours(36);

            var sql = @"
                SELECT 
                    t.dept_name as DeptName, t.dept_code as DeptCode, t.pin as PIN,
                    ISNULL(t.last_name, '') + ' ' + ISNULL(t.name, '') as FullName,
                    t.dev_alias as DevAlias, t.event_point_name as EventPointName,
                    t.event_time as EventTime, t.event_no as EventNo
                FROM acc_transaction t
                WHERE t.pin = @Pin AND t.event_time BETWEEN @Start AND @End AND t.event_no <> 6";

            var pinTransactions = (await connection.QueryAsync<dynamic>(sql, new { Pin = pin, Start = queryStart, End = queryEnd })).ToList();
            if (!pinTransactions.Any())
                continue;

            var deptMapByCode = departmentCache.ByCode;
            var deptMapByName = departmentCache.ByName;

            // Verify this person belongs to an allowed department
            bool isAllowed = pinTransactions.Any(t =>
            {
                var dId = DepartmentHelper.ResolveDeptId(
                    t.DeptCode != null ? (string)t.DeptCode : null,
                    t.DeptName != null ? (string)t.DeptName : null,
                    deptMapByCode, deptMapByName, departments);
                return dId != null && allowedDepts.Contains(dId);
            });

            if (!isAllowed)
                continue;

            for (var date = fromDate; date <= toDate; date = date.AddDays(1))
            {
                var reportStart = date.AddHours(4);
                var reportEnd   = reportStart.AddDays(1);

                var dailyTransactions = pinTransactions
                    .Where(t => (DateTime)t.EventTime >= reportStart && (DateTime)t.EventTime < reportEnd)
                    .ToList();

                AttendanceRecord? record;

                if (!dailyTransactions.Any())
                {
                    var info = pinTransactions.First();
                    record = new AttendanceRecord
                    {
                        Date = date, Pin = pin,
                        FullName  = (string)info.FullName,
                        DeptName  = (string)info.DeptName,
                        Evaluation = ""
                    };
                }
                else
                {
                    record = BuildAttendanceRecord(
                        pin, date.Date, pinTransactions,
                        reportStart, reportEnd,
                        deptDict, rootNames, deptMapByCode, deptMapByName, factoryCluster);
                }

                if (record != null)
                    allRecords.Add(record);
            }
        }

        return allRecords.GroupBy(r => r.Date).Select(g => g.First()).OrderBy(r => r.Date);
    }

    // -------------------------------------------------------------------------
    // Contractor Report
    // -------------------------------------------------------------------------

    public async Task<IEnumerable<AttendanceRecord>> GetContractorsReportAsync(
        DateTime date, string? pin = null, string? factory = null)
    {
        var reportStart = date.Date.AddHours(4);
        var reportEnd   = reportStart.AddDays(1);
        var queryStart  = reportStart.AddHours(-12);
        var queryEnd    = reportStart.AddHours(36);

        var allRecords = new List<AttendanceRecord>();

        foreach (var connStr in GetConnectionStrings())
        {
            using var connection = new SqlConnection(connStr);

            var departmentCache = await GetDepartmentCacheEntryAsync(connStr, connection);
            var departments  = departmentCache.Departments;
            var deptDict     = departmentCache.ById;
            var serverIp     = DepartmentHelper.GetServerIp(connStr);
            var factoryCluster = ResolveFactoryClusterByServerIp(serverIp);
            var rootNames    = ServerConstants.GetPersonalRootNames(serverIp);

            // Contractor mode: only include CUS/TMP/SUP sub-departments
            var allowedDepts = DepartmentHelper.BuildAllowedDepartments(departments, rootNames, contractorMode: true);
            if (!allowedDepts.Any())
                continue;

            var transactions = await FetchTransactions(connection, queryStart, queryEnd, pin);
            if (!transactions.Any())
                continue;

            var deptMapByCode = departmentCache.ByCode;
            var deptMapByName = departmentCache.ByName;

            // Step 1: find PINs that belong to allowed contractor departments.
            var allowedPins = transactions
                .Select(t => new
                {
                    Trans  = t,
                    DeptId = DepartmentHelper.ResolveDeptId(
                        t.DeptCode != null ? (string)t.DeptCode : null,
                        t.DeptName != null ? (string)t.DeptName : null,
                        deptMapByCode, deptMapByName, departments)
                })
                .Where(x => x.DeptId != null && allowedDepts.Contains(x.DeptId!))
                .Select(x => (string)x.Trans.PIN)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Step 2: build records from ALL transactions for those PINs.
            var records = transactions
                .Where(t => allowedPins.Contains((string)t.PIN))
                .GroupBy(t => (string)t.PIN)
                .Select(g => BuildContractorRecord(
                    g.Key, date.Date,
                    g.OrderBy(t => (DateTime)t.EventTime).ToList(),
                    reportStart, reportEnd,
                    deptDict, rootNames, deptMapByCode, deptMapByName, factoryCluster))
                .Where(r => r != null && (string.IsNullOrEmpty(factory) || r!.Factory == factory))
                .ToList();

            allRecords.AddRange(records!);
        }

        return allRecords;
    }

    // -------------------------------------------------------------------------
    // Transaction Detail (for modal popup)
    // -------------------------------------------------------------------------

    public async Task<IEnumerable<AccTransaction>> GetTransactionsByRangeAsync(string pin, DateTime start, DateTime end)
    {
        const string query = @"
            SELECT 
                event_time        AS EventTime,
                area_name         AS AreaName,
                dept_name         AS DeptName,
                pin               AS Pin,
                ISNULL(last_name, '') + ' ' + ISNULL(name, '') AS FullName,
                dev_alias         AS DevAlias,
                event_point_name  AS EventPointName,
                event_no          AS EventNo,
                verify_mode_no    AS VerifyModeNo,
                CASE 
                    WHEN event_no IN (0, 1) OR verify_mode_no = 2048 THEN 'Normal' 
                    WHEN event_no IN (27, 41)                         THEN 'Exception' 
                    ELSE 'Other' 
                END AS Status,
                CASE 
                    WHEN verify_mode_no = 18  THEN 'Face + Card' 
                    WHEN verify_mode_no = 16  THEN 'Face + Fingerprint' 
                    WHEN verify_mode_no = 1   THEN 'Only Fingerprint' 
                    WHEN verify_mode_no = 200 THEN 'Card' 
                    WHEN verify_mode_no = 4   THEN 'Only Card' 
                    WHEN verify_mode_no = 2048 THEN 'Card' 
                    ELSE CAST(verify_mode_no AS VARCHAR) 
                END AS VerifyModeDisplay,
                CASE 
                    WHEN event_point_name LIKE '%-OUT%' OR event_point_name LIKE '% OUT%' OR
                         event_point_name LIKE '%OUT-%' OR event_point_name LIKE '%EXIT%' THEN 'GATE OUT'
                    WHEN event_point_name LIKE '%-IN%' OR event_point_name LIKE '% IN%' OR
                         event_point_name LIKE '%IN-%' OR event_point_name LIKE '%ENTRY%' THEN 'GATE IN'
                    WHEN event_point_name LIKE '%ATT%'                                     THEN 'ATTENDANCE'
                    WHEN event_point_name LIKE '%ACS-FF%'                                  THEN 'VERIFY'
                    ELSE 'Other'
                END AS Type
            FROM acc_transaction
            WHERE pin = @Pin AND event_time BETWEEN @Start AND @End AND event_no <> 6
            ORDER BY event_time ASC";

        var tasks = GetConnectionStrings().Select(async connStr =>
        {
            using var connection = new SqlConnection(connStr);
            return await connection.QueryAsync<AccTransaction>(query, new { Pin = pin, Start = start, End = end });
        });

        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).OrderBy(t => t.EventTime);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>Fetches and caches department metadata used across reports.</summary>
    private async Task<DepartmentCacheEntry> GetDepartmentCacheEntryAsync(string connStr, SqlConnection connection)
    {
        string cacheKey = $"Departments::{connStr}";
        if (_cache.TryGetValue<DepartmentCacheEntry>(cacheKey, out var cachedDepartments))
            return cachedDepartments!;

        var departments = (await connection.QueryAsync<Department>(
            "SELECT id AS Id, code AS Code, name AS Name, parent_id AS ParentId FROM auth_department")).ToList();
        var (byCode, byName) = BuildDeptMaps(departments);
        var cacheEntry = new DepartmentCacheEntry(
            departments,
            departments.ToDictionary(d => d.Id),
            byCode,
            byName);

        _cache.Set(cacheKey, cacheEntry, DepartmentCacheDuration);
        return cacheEntry;
    }

    /// <summary>Fetches raw transaction logs within the given time window, optionally filtered by PIN.</summary>
    private static async Task<List<dynamic>> FetchTransactions(
        SqlConnection connection, DateTime start, DateTime end, string? pin)
    {
        var sql = @"
            SELECT 
                t.dept_name         as DeptName, 
                t.dept_code         as DeptCode,
                t.pin               as PIN,
                ISNULL(t.last_name, '') + ' ' + ISNULL(t.name, '') as FullName,
                t.dev_alias         as DevAlias,
                t.event_point_name  as EventPointName,
                t.event_time        as EventTime,
                t.event_no          as EventNo
            FROM acc_transaction t
            WHERE t.event_time BETWEEN @StartTime AND @EndTime AND t.event_no <> 6";

        if (!string.IsNullOrEmpty(pin))
            sql += " AND t.pin LIKE @Pin";

        sql += " ORDER BY t.pin, t.event_time ASC";

        var rows = await connection.QueryAsync<dynamic>(sql, new { StartTime = start, EndTime = end, Pin = $"%{pin}%" });
        return rows.ToList();
    }

    /// <summary>
    /// Builds two lookup dictionaries for fast department resolution:
    /// one by Code and one by Name (case-insensitive).
    /// </summary>
    private static (Dictionary<string, string> ByCode, Dictionary<string, string> ByName) BuildDeptMaps(
        List<Department> departments)
    {
        var byCode = departments
            .Where(d => !string.IsNullOrEmpty(d.Code))
            .GroupBy(d => d.Code)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var byName = departments
            .Where(d => !string.IsNullOrEmpty(d.Name))
            .GroupBy(d => d.Name.Trim())
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        return (byCode, byName);
    }

    private static string BuildAllTransactionsCacheKey(int pageIndex, int pageSize, TransactionFilter filter)
    {
        return string.Join("::",
            "AllTransactions",
            "ExcludePinKH_TT_SU_v2",
            pageIndex,
            pageSize,
            filter.FromDate?.ToString("yyyyMMddHHmmss") ?? "",
            filter.ToDate?.ToString("yyyyMMddHHmmss") ?? "",
            filter.AreaName ?? "",
            filter.DeptName ?? "",
            filter.Pin ?? "",
            filter.Name ?? "",
            filter.DevAlias ?? "",
            filter.EventPoint ?? "",
            filter.Status ?? "");
    }

    private static bool HasExcludedPinToken(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
            return false;

        var normalizedPin = pin.Trim().ToUpperInvariant();
        return ExcludedPinTokens.Any(token => normalizedPin.Contains(token));
    }

    private static string ResolveFactoryClusterByServerIp(string serverIp)
    {
        if (serverIp.Contains(ServerConstants.Server045IpPattern))
            return "JIAHSIN";
        if (serverIp.Contains(ServerConstants.Server345IpPattern))
            return "SHIMMER";
        if (serverIp.Contains(ServerConstants.Server046IpPattern))
            return "JT";
        return "";
    }

    /// <summary>
    /// Resolves the Factory name and BU name from a department ID.
    /// JSGS is grouped under SHIMMER; JIAHSIN departments are further resolved to BU1/BU2/BU3.
    /// </summary>
    private static (string Factory, string BU) ResolveFactoryAndBU(
        string? deptId,
        Dictionary<string, Department> deptDict,
        IEnumerable<string> rootNames)
    {
        if (string.IsNullOrEmpty(deptId))
            return ("", "");

        var root        = DepartmentHelper.GetRootDepartment(deptId, deptDict, rootNames);
        var factoryName = root?.Name ?? "";

        // Normalize: JSGS is reported under SHIMMER
        if (factoryName.Equals("JSGS", StringComparison.OrdinalIgnoreCase))
        {
            factoryName = "SHIMMER";
        }

        var buName = factoryName;
        if (root != null && factoryName.Equals("JIAHSIN", StringComparison.OrdinalIgnoreCase))
        {
            var buRoot   = DepartmentHelper.GetBUDepartment(deptId, deptDict, root.Id);
            var buSubName = buRoot?.Name ?? "";

            buName = buSubName switch
            {
                var n when n.Contains("BU1", StringComparison.OrdinalIgnoreCase) => "BU1",
                var n when n.Contains("BU2", StringComparison.OrdinalIgnoreCase) => "BU2",
                var n when n.Contains("BU3", StringComparison.OrdinalIgnoreCase) => "BU3",
                var n when n.Contains("JSG", StringComparison.OrdinalIgnoreCase) => "JSG",
                _ => "JIAHSIN"
            };
        }
        else if (root != null && factoryName.Equals("JSG", StringComparison.OrdinalIgnoreCase))
        {
            // JSG is a BU under JIAHSIN (peer of BU1/BU2/BU3)
            factoryName = "JIAHSIN";
            buName = "JSG";
        }

        return (factoryName, buName);
    }

    /// <summary>
    /// Processes all transactions for one employee PIN and produces an AttendanceRecord.
    /// Returns null if no valid shift start punch is found.
    /// </summary>
    private AttendanceRecord? BuildAttendanceRecord(
        string pin, DateTime reportDate,
        List<dynamic> allPinTransactions,
        DateTime reportStart, DateTime reportEnd,
        Dictionary<string, Department> deptDict,
        IEnumerable<string> rootNames,
        Dictionary<string, string> deptMapByCode,
        Dictionary<string, string> deptMapByName,
        string factoryCluster)
    {
        var dailyTransactions = allPinTransactions
            .Where(t => (DateTime)t.EventTime >= reportStart && (DateTime)t.EventTime < reportEnd)
            .ToList();

        if (!dailyTransactions.Any())
            return null;

        var shiftStartPunch = AttendanceHelper.FindShiftStartPunch(dailyTransactions, allPinTransactions)
                           ?? dailyTransactions.First();

        var shiftStartTime      = (DateTime)shiftStartPunch.EventTime;
        var shiftEndTime        = (DateTime)allPinTransactions.OrderByDescending(t => t.EventTime).First().EventTime;
        var stayDurationMinutes = (shiftEndTime - shiftStartTime).TotalMinutes;
        var punchDate           = shiftStartTime.Date;

        bool isJSGAR = shiftStartPunch.DeptCode != null &&
                       ((string)shiftStartPunch.DeptCode).Contains("JSGAR", StringComparison.OrdinalIgnoreCase);

        var shiftDefs    = AttendanceHelper.GetShiftDefinitions(punchDate, includeShift4: isJSGAR);
        var matchedShift = AttendanceHelper.MatchShift(shiftStartTime, stayDurationMinutes, shiftDefs);

        // For day shifts, cap logs at end of day; Ca 3 extends to 06:30 next morning
        var shiftLimitTime = matchedShift.Name == "Ca 3"
            ? punchDate.AddDays(1).AddHours(6).AddMinutes(30)
            : punchDate.AddDays(1).AddTicks(-1);

        var currentShiftLogs = allPinTransactions
            .Where(t => (DateTime)t.EventTime >= shiftStartTime && (DateTime)t.EventTime <= shiftLimitTime)
            .ToList();

        var (acsLogs, attLogs) = AttendanceHelper.SeparateLogsByDevice(currentShiftLogs);
        var (gateIn, gateOut)  = AttendanceHelper.ExtractGatePunches(acsLogs);
        // dynamic cannot be deconstructed from a tuple — assign explicitly
        var attendPunches = AttendanceHelper.ResolveAttendancePunches(attLogs, gateIn, gateOut, matchedShift);
        dynamic? attendIn = attendPunches.Item1;
        dynamic? attendOut = attendPunches.Item2;
        var gatePunches = AttendanceHelper.ValidatePunchSequence(gateIn, attendIn, attendOut, gateOut);
        gateIn  = gatePunches.Item1;
        gateOut = gatePunches.Item2;

        var deptIdForFactory = DepartmentHelper.ResolveDeptId(
            shiftStartPunch.DeptCode != null ? (string)shiftStartPunch.DeptCode : null,
            shiftStartPunch.DeptName != null ? (string)shiftStartPunch.DeptName : null,
            deptMapByCode, deptMapByName, deptDict.Values.ToList());

        var (factoryName, buName) = ResolveFactoryAndBU(deptIdForFactory, deptDict, rootNames);

        var evaluations = AttendanceHelper.EvaluateAttendance(
            gateIn, attendIn, attendOut, gateOut, matchedShift, shiftStartTime, isJSGAR);

        return new AttendanceRecord
        {
            DeptName    = (string)shiftStartPunch.DeptName,
            Pin         = pin,
            FullName    = (string)shiftStartPunch.FullName,
            Date        = reportDate,
            Factory     = factoryName,
            FactoryCluster = factoryCluster,
            BU          = buName,
            GateIn      = gateIn?.EventTime,
            AttendIn    = attendIn?.EventTime,
            AttendOut   = attendOut?.EventTime,
            GateOut     = gateOut?.EventTime,
            FirstPunch  = currentShiftLogs.First().EventTime,
            LastPunch   = currentShiftLogs.Last().EventTime,
            Shift       = matchedShift.Name,
            Evaluation  = AttendanceHelper.FormatEvaluation(evaluations)
        };
    }

    /// <summary>
    /// Simplified record builder for contractors: always uses "Hành chính" shift,
    /// only checks for missing punches (B evaluation).
    /// </summary>
    private AttendanceRecord? BuildContractorRecord(
        string pin, DateTime reportDate,
        List<dynamic> allPinTransactions,
        DateTime reportStart, DateTime reportEnd,
        Dictionary<string, Department> deptDict,
        IEnumerable<string> rootNames,
        Dictionary<string, string> deptMapByCode,
        Dictionary<string, string> deptMapByName,
        string factoryCluster)
    {
        var dailyTransactions = allPinTransactions
            .Where(t => (DateTime)t.EventTime >= reportStart && (DateTime)t.EventTime < reportEnd)
            .ToList();

        if (!dailyTransactions.Any())
            return null;

        var shiftStartPunch = AttendanceHelper.FindShiftStartPunch(dailyTransactions, allPinTransactions)
                           ?? dailyTransactions.First();

        var shiftStartTime = (DateTime)shiftStartPunch.EventTime;
        var punchDate      = shiftStartTime.Date;

        // Contractors always work the administrative shift
        var contractorShift = new ShiftDefinition("Hành chính",
            punchDate.AddHours(7).AddMinutes(30),
            punchDate.AddHours(16).AddMinutes(30));

        var shiftLimitTime = punchDate.AddDays(1).AddTicks(-1);
        var currentShiftLogs = allPinTransactions
            .Where(t => (DateTime)t.EventTime >= shiftStartTime && (DateTime)t.EventTime <= shiftLimitTime)
            .ToList();

        var (acsLogs, attLogs) = AttendanceHelper.SeparateLogsByDevice(currentShiftLogs);
        var (gateIn, gateOut)  = AttendanceHelper.ExtractGatePunches(acsLogs);

        dynamic? attendIn  = attLogs.FirstOrDefault();
        dynamic? attendOut = attLogs.Count > 1 ? attLogs.Last() : null;

        // If single ATT punch, treat as In only
        if (attendIn != null && attendOut != null && attendIn.EventTime == attendOut.EventTime)
            attendOut = null;

        var deptIdForFactory = DepartmentHelper.ResolveDeptId(
            shiftStartPunch.DeptCode != null ? (string)shiftStartPunch.DeptCode : null,
            shiftStartPunch.DeptName != null ? (string)shiftStartPunch.DeptName : null,
            deptMapByCode, deptMapByName, deptDict.Values.ToList());

        var (factoryName, buName) = ResolveFactoryAndBU(deptIdForFactory, deptDict, rootNames);

        // Contractors only get B if any punch is missing
        var evaluation = (gateIn == null || attendIn == null || gateOut == null || attendOut == null)
            ? "[B]"
            : "";

        return new AttendanceRecord
        {
            DeptName   = (string)shiftStartPunch.DeptName,
            Pin        = pin,
            FullName   = (string)shiftStartPunch.FullName,
            Date       = reportDate,
            Factory    = factoryName,
            FactoryCluster = factoryCluster,
            BU         = buName,
            GateIn     = gateIn?.EventTime,
            AttendIn   = attendIn?.EventTime,
            AttendOut  = attendOut?.EventTime,
            GateOut    = gateOut?.EventTime,
            FirstPunch = currentShiftLogs.First().EventTime,
            LastPunch  = currentShiftLogs.Last().EventTime,
            Shift      = contractorShift.Name,
            Evaluation = evaluation
        };
    }

    // -------------------------------------------------------------------------
    // Early Exit Report (Attend In → First Gate Out ≤ threshold minutes)
    // -------------------------------------------------------------------------

    public async Task<IEnumerable<EarlyExitRecord>> GetEarlyExitReportAsync(
        DateTime date, int thresholdMinutes = 1, string? factory = null)
    {
        // Report window: full day + night extension (22:00 selected day → 06:00 next day)
        var reportStart = date.Date;
        var reportEnd   = date.Date.AddDays(1).AddHours(6);
        var queryStart  = reportStart.AddHours(-12);
        var queryEnd    = reportEnd.AddHours(12);

        var allRecords = new List<EarlyExitRecord>();

        foreach (var connStr in GetConnectionStrings())
        {
            using var connection = new SqlConnection(connStr);

            var departmentCache = await GetDepartmentCacheEntryAsync(connStr, connection);
            var departments  = departmentCache.Departments;
            var deptDict     = departmentCache.ById;
            var serverIp     = DepartmentHelper.GetServerIp(connStr);
            var rootNames    = ServerConstants.GetAttendanceRootNames(serverIp);
            var allowedDepts = DepartmentHelper.BuildAllowedDepartments(departments, rootNames);

            if (!allowedDepts.Any())
                continue;

            // Fetch all transactions (ATT + ACS) for the time window
            var transactions = await FetchTransactions(connection, queryStart, queryEnd, pin: null);
            if (!transactions.Any())
                continue;

            var deptMapByCode = departmentCache.ByCode;
            var deptMapByName = departmentCache.ByName;

            // Filter to employees in allowed departments, then process per PIN
            var byPin = transactions
                .Select(t => new
                {
                    Trans  = t,
                    DeptId = DepartmentHelper.ResolveDeptId(
                        t.DeptCode != null ? (string)t.DeptCode : null,
                        t.DeptName != null ? (string)t.DeptName : null,
                        deptMapByCode, deptMapByName, departments)
                })
                .Where(x => x.DeptId != null && allowedDepts.Contains(x.DeptId!))
                .GroupBy(x => (string)x.Trans.PIN);

            foreach (var group in byPin)
            {
                var pin     = group.Key;
                var allLogs = group.OrderBy(x => (DateTime)x.Trans.EventTime).Select(x => x.Trans).ToList();

                // Only consider logs within today's report window
                var windowLogs = allLogs
                    .Where(t => (DateTime)t.EventTime >= reportStart && (DateTime)t.EventTime < reportEnd)
                    .ToList();

                if (!windowLogs.Any())
                    continue;

                // Determine shift for this employee (day or night)
                var shiftStartPunch = AttendanceHelper.FindShiftStartPunch(windowLogs, allLogs) ?? windowLogs.First();
                var shiftStartTime  = (DateTime)shiftStartPunch.EventTime;
                bool isJSGAR        = shiftStartPunch.DeptCode != null &&
                                      ((string)shiftStartPunch.DeptCode).Contains("JSGAR", StringComparison.OrdinalIgnoreCase);
                var shiftDefs       = AttendanceHelper.GetShiftDefinitions(shiftStartTime.Date, includeShift4: isJSGAR);
                var stayMinutes     = allLogs.Count > 1
                    ? ((DateTime)allLogs.Last().EventTime - shiftStartTime).TotalMinutes
                    : 0;
                var matchedShift    = AttendanceHelper.MatchShift(shiftStartTime, stayMinutes, shiftDefs);

                // Focus on logs during the matched shift window
                var shiftEndTime = matchedShift.End;
                var currentShiftLogs = allLogs
                    .Where(t => (DateTime)t.EventTime >= shiftStartTime && (DateTime)t.EventTime <= shiftEndTime)
                    .ToList();

                var (acsLogs, attLogs) = AttendanceHelper.SeparateLogsByDevice(currentShiftLogs);
                var (gateIn, gateOut) = AttendanceHelper.ExtractGatePunches(acsLogs);

                var attendPunches = AttendanceHelper.ResolveAttendancePunches(attLogs, gateIn, gateOut, matchedShift);
                dynamic? attendInLog = attendPunches.Item1;
                dynamic? attendOutLog = attendPunches.Item2;

                // For night shift, avoid treating a 06:00 punch as AttendIn
                if (matchedShift.Name == "Ca 3")
                {
                    if (attendInLog != null &&
                        (DateTime)attendInLog.EventTime >= matchedShift.End.AddMinutes(-30))
                    {
                        attendOutLog ??= attendInLog;
                        attendInLog = null;
                    }

                    if (attendInLog == null)
                    {
                        var latestValidIn = matchedShift.End.AddMinutes(-30);
                        attendInLog = attLogs
                            .FirstOrDefault(t => (DateTime)t.EventTime >= matchedShift.Start &&
                                                 (DateTime)t.EventTime < latestValidIn);
                    }
                }

                if (attendInLog == null)
                    continue;

                var attendInTime = (DateTime)attendInLog.EventTime;

                // First gate-out = first ACS OUT after AttendIn (within window)
                var firstGateOut = AttendanceHelper.FindFirstGateOut(acsLogs, attendInTime);
                if (firstGateOut == null)
                    continue;

                var gapMinutes = ((DateTime)firstGateOut.EventTime - attendInTime).TotalMinutes;
                if (gapMinutes > thresholdMinutes)
                    continue;

                var infoPunch = windowLogs.First();
                var deptId    = DepartmentHelper.ResolveDeptId(
                    infoPunch.DeptCode != null ? (string)infoPunch.DeptCode : null,
                    infoPunch.DeptName != null ? (string)infoPunch.DeptName : null,
                    deptMapByCode, deptMapByName, deptDict.Values.ToList());

                var (factoryName, buName) = ResolveFactoryAndBU(deptId, deptDict, rootNames);

                bool isFactoryMatch = string.IsNullOrEmpty(factory) || 
                                     factoryName == factory || 
                                     (factory == "JIAHSIN" && factoryName == "JSG");

                if (!isFactoryMatch)
                    continue;

                allRecords.Add(new EarlyExitRecord
                {
                    Pin          = pin,
                    FullName     = (string)infoPunch.FullName,
                    DeptName     = (string)infoPunch.DeptName,
                    Factory      = factoryName,
                    BU           = buName,
                    Date         = date.Date,
                    Shift        = matchedShift.Name,
                    AttendIn     = attendInTime,
                    FirstGateOut = (DateTime)firstGateOut.EventTime
                });
            }
        }

        return allRecords.OrderBy(r => r.Factory).ThenBy(r => r.DeptName).ThenBy(r => r.FullName);
    }
}


