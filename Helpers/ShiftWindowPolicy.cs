namespace ZkbioDashboard.Helpers;

public static class ShiftWindowPolicy
{
    private static readonly TimeSpan ReportStartOffset = TimeSpan.FromHours(4);
    private static readonly TimeSpan QueryBackwardOffset = TimeSpan.FromHours(12);
    private static readonly TimeSpan QueryForwardOffset = TimeSpan.FromHours(36);
    private static readonly TimeSpan AccessHistoryDetailTail = TimeSpan.FromHours(2);
    private static readonly TimeSpan EarlyExitDetailTail = TimeSpan.FromHours(6);

    public static (DateTime ReportStart, DateTime ReportEnd) GetDailyReportWindow(DateTime reportDate)
    {
        var reportStart = reportDate.Date.Add(ReportStartOffset);
        return (reportStart, reportStart.AddDays(1));
    }

    public static (DateTime QueryStart, DateTime QueryEnd) GetDailyQueryWindow(DateTime reportDate)
    {
        var (reportStart, _) = GetDailyReportWindow(reportDate);
        return (reportStart.Subtract(QueryBackwardOffset), reportStart.Add(QueryForwardOffset));
    }

    public static (DateTime QueryStart, DateTime QueryEnd) GetDateRangeQueryWindow(DateTime fromDate, DateTime toDate)
    {
        var start = fromDate.Date.Subtract(QueryBackwardOffset);
        var end = toDate.Date.AddDays(1).Add(QueryForwardOffset);
        return (start, end);
    }

    public static (DateTime Start, DateTime End) GetAccessHistoryDetailWindow(DateTime shiftDate)
    {
        var (reportStart, reportEnd) = GetDailyReportWindow(shiftDate);
        return (reportStart, reportEnd.Add(AccessHistoryDetailTail));
    }

    public static (DateTime Start, DateTime End) GetEarlyExitDetailWindow(DateTime selectedDate)
    {
        var start = selectedDate.Date;
        return (start, start.AddDays(1).Add(EarlyExitDetailTail));
    }
}
