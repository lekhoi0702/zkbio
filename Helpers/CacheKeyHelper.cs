using System;
using System.Collections.Generic;

namespace ZkbioDashboard.Helpers;

public static class CacheKeyHelper
{
    public static string AttendanceRaw(DateTime reportDate, string? pin) =>
        $"Attendance_Raw_{reportDate:yyyyMMdd}_{pin ?? ""}";

    public static string AttendanceFiltered(
        DateTime reportDate,
        string? pin,
        string? factory,
        string? bu,
        IEnumerable<string>? selectedTypes)
    {
        var typesStr = selectedTypes is null ? "" : string.Join("-", selectedTypes);
        return $"Attendance_Filtered_{reportDate:yyyyMMdd}_{pin ?? ""}_{factory}_{bu}_{typesStr}";
    }

    public static string ContractorAttendance(DateTime fromDate, DateTime toDate, string? pin) =>
        $"Contractor_Attendance_{fromDate:yyyyMMddHHmmss}_{toDate:yyyyMMddHHmmss}_{pin ?? ""}";

    public static string EarlyExit(DateTime reportDate, int thresholdMinutes, string? factory, string? bu) =>
        $"EarlyExit_{reportDate:yyyyMMdd}_{thresholdMinutes}_{factory}_{bu}";
}

