using Dapper;
using ZkbioDashboard.Models;

namespace ZkbioDashboard.Helpers;

/// <summary>
/// Helper for building reusable SQL queries and WHERE clauses for acc_transaction.
/// </summary>
public static class TransactionQueryHelper
{
    /// <summary>
    /// Standard SELECT columns for acc_transaction, with computed Status and VerifyModeDisplay.
    /// </summary>
    public const string TransactionSelectSql = @"
        SELECT 
            event_time        AS EventTime,
            area_name         AS AreaName,
            dept_code         AS DeptCode,
            dept_name         AS DeptName,
            pin               AS Pin,
            ISNULL(last_name, '') + ' ' + ISNULL(name, '') AS FullName,
            dev_alias         AS DevAlias,
            event_point_name  AS EventPointName,
            event_no          AS EventNo,
            verify_mode_no    AS VerifyModeNo,
            CASE 
                WHEN event_no IN (27, 41)                         THEN 'Exception' 
                WHEN event_no IN (0, 1) OR verify_mode_no = 2048 THEN 'Normal' 
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
            END AS VerifyModeDisplay
        FROM acc_transaction";

    /// <summary>
    /// Base WHERE conditions applied to every transaction query:
    /// excludes door-held events (event_no=6) and unverified swipes (verify_mode_no=0).
    /// </summary>
    private static readonly List<string> BaseWhereClauses =
    [
        "event_no <> 6",
        "verify_mode_no <> 0",
        "(pin IS NULL OR UPPER(CAST(pin AS NVARCHAR(50))) NOT LIKE '%KH%')",
        "(pin IS NULL OR UPPER(CAST(pin AS NVARCHAR(50))) NOT LIKE '%TT%')",
        "(pin IS NULL OR UPPER(CAST(pin AS NVARCHAR(50))) NOT LIKE '%SU%')"
    ];

    /// <summary>
    /// Builds a WHERE clause and Dapper parameter bag from a TransactionFilter.
    /// Returns (whereSql, parameters) ready to append to a query.
    /// </summary>
    public static (string WhereSql, DynamicParameters Parameters) BuildWhereClause(TransactionFilter filter)
    {
        var clauses    = new List<string>(BaseWhereClauses);
        var parameters = new DynamicParameters();

        if (filter.FromDate.HasValue)
        {
            clauses.Add("event_time >= @FromDate");
            parameters.Add("FromDate", filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            clauses.Add("event_time <= @ToDate");
            parameters.Add("ToDate", filter.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.AreaName))
        {
            clauses.Add("area_name LIKE @AreaName");
            parameters.Add("AreaName", $"%{filter.AreaName}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.DeptName))
        {
            clauses.Add("dept_name LIKE @DeptName");
            parameters.Add("DeptName", $"%{filter.DeptName}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.Pin))
        {
            clauses.Add("pin LIKE @Pin");
            parameters.Add("Pin", $"%{filter.Pin}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            clauses.Add("name LIKE @Name");
            parameters.Add("Name", $"%{filter.Name}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.DevAlias))
        {
            clauses.Add("dev_alias LIKE @DevAlias");
            parameters.Add("DevAlias", $"%{filter.DevAlias}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.EventPoint))
        {
            clauses.Add("event_point_name LIKE @EventPoint");
            parameters.Add("EventPoint", $"%{filter.EventPoint}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var statusClause = filter.Status switch
            {
                "Normal"    => "((event_no IN (0, 1) OR verify_mode_no = 2048) AND event_no NOT IN (27, 41))",
                "Exception" => "event_no IN (27, 41)",
                _           => null
            };

            if (statusClause != null)
                clauses.Add(statusClause);
        }

        var whereSql = " WHERE " + string.Join(" AND ", clauses);
        return (whereSql, parameters);
    }
}
