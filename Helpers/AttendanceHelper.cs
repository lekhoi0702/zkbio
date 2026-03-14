using ZkbioDashboard.Models;

namespace ZkbioDashboard.Helpers;

/// <summary>
/// Shift definition with a name, start, and end time.
/// </summary>
public record ShiftDefinition(string Name, DateTime Start, DateTime End);

/// <summary>
/// Resolved attendance punch times: when the employee entered/exited the gate
/// and when they clocked in/out on the attendance machine.
/// </summary>
public record AttendancePunches(
    dynamic? GateIn,
    dynamic? AttendIn,
    dynamic? AttendOut,
    dynamic? GateOut);

/// <summary>
/// Static helper for attendance report logic: shift matching, punch resolution, and evaluation.
/// Shared by GetAttendanceReportAsync, GetPersonalAttendanceReportAsync.
/// </summary>
public static class AttendanceHelper
{
    /// <summary>
    /// Returns the standard shift definitions for a given punch date.
    /// Ca 4 is only included when isJSGAR is true (early-shift department).
    /// </summary>
    public static ShiftDefinition[] GetShiftDefinitions(DateTime punchDate, bool includeShift4)
    {
        var shifts = new List<ShiftDefinition>
        {
            new("Hành chính", punchDate.AddHours(7).AddMinutes(30), punchDate.AddHours(16).AddMinutes(30)),
            new("Ca 1",       punchDate.AddHours(6),                punchDate.AddHours(14)),
            new("Ca 2",       punchDate.AddHours(14),               punchDate.AddHours(22)),
            new("Ca 3",       punchDate.AddHours(22),               punchDate.AddDays(1).AddHours(6)),
        };

        if (includeShift4)
            shifts.Add(new("Ca 4", punchDate.AddHours(5).AddMinutes(30), punchDate.AddHours(14).AddMinutes(30)));

        return [.. shifts];
    }

    /// <summary>
    /// Matches the best shift for a given punch start time and stay duration.
    /// Morning arrivals are biased toward Hành chính; short stays use nearest endpoint.
    /// </summary>
    public static ShiftDefinition MatchShift(DateTime shiftStartTime, double stayMinutes, ShiftDefinition[] shifts)
    {
        return shifts.OrderBy(s =>
        {
            double distToStart = Math.Abs((shiftStartTime - s.Start).TotalMinutes);
            double distToEnd   = Math.Abs((shiftStartTime - s.End).TotalMinutes);

            // Short stay: employee only punched near one endpoint
            double effectiveDist = stayMinutes < 60 ? Math.Min(distToStart, distToEnd) : distToStart;

            if (shiftStartTime.Hour < 12)
            {
                if (s.Name == "Hành chính") return effectiveDist * 0.5; // Prefer admin shift in morning
                if (s.Start.Hour < 10)       return effectiveDist;        // Early shifts at normal weight
                return effectiveDist * 2.0;                               // De-prioritize afternoon shifts
            }

            return effectiveDist;
        }).First();
    }

    /// <summary>
    /// Finds the first punch of the current shift — the punch with no preceding punch
    /// within the last 12 hours (i.e., a fresh start, not a continuation).
    /// </summary>
    public static dynamic? FindShiftStartPunch(List<dynamic> dailyTransactions, List<dynamic> allTransactions)
    {
        foreach (var punch in dailyTransactions)
        {
            var punchTime = (DateTime)punch.EventTime;
            var hasPrecedingPunch = allTransactions.Any(t =>
                (DateTime)t.EventTime < punchTime &&
                (punchTime - (DateTime)t.EventTime).TotalHours < 12);

            if (!hasPrecedingPunch)
                return punch;
        }

        return null;
    }

    /// <summary>
    /// Separates the current shift's logs into ACS (gate) and ATT (attendance machine) logs.
    /// </summary>
    public static (List<dynamic> AcsLogs, List<dynamic> AttLogs) SeparateLogsByDevice(List<dynamic> shiftLogs)
    {
        var acsLogs = shiftLogs
            .Where(t => t.DevAlias != null && ((string)t.DevAlias).Contains("ACS", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var attLogs = shiftLogs
            .Where(t => t.DevAlias != null && ((string)t.DevAlias).Contains("ATT", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return (acsLogs, attLogs);
    }

    /// <summary>
    /// Extracts gate-in and gate-out from ACS logs by looking for IN/ENTRY and OUT/EXIT in the event point name.
    /// </summary>
    public static (dynamic? GateIn, dynamic? GateOut) ExtractGatePunches(List<dynamic> acsLogs)
    {
        var gateIn = acsLogs.FirstOrDefault(t =>
            t.EventPointName != null &&
            (((string)t.EventPointName).Contains("IN", StringComparison.OrdinalIgnoreCase) ));

        var gateOut = acsLogs.LastOrDefault(t =>
            t.EventPointName != null &&
            (((string)t.EventPointName).Contains("OUT", StringComparison.OrdinalIgnoreCase) 
             ));

        return (gateIn, gateOut);
    }

    /// <summary>
    /// Returns the FIRST gate-out punch that occurs AFTER the given reference time (e.g. AttendIn).
    /// Used by EarlyExit detection to find the earliest exit after clocking in.
    /// </summary>
    public static dynamic? FindFirstGateOut(List<dynamic> acsLogs, DateTime afterTime)
    {
        return acsLogs
            .Where(t =>
                t.EventPointName != null &&
                (((string)t.EventPointName).Contains("OUT", StringComparison.OrdinalIgnoreCase) ||
                 ((string)t.EventPointName).Contains("EXIT", StringComparison.OrdinalIgnoreCase)) &&
                (DateTime)t.EventTime > afterTime)
            .MinBy(t => (DateTime)t.EventTime);
    }


    /// <summary>
    /// Smart consolidation: when ATT punches cluster rapidly (&lt;10 min apart) or are a single punch,
    /// uses gate context to decide whether the cluster is an entry or exit event.
    /// </summary>
    public static (dynamic? AttendIn, dynamic? AttendOut) ResolveAttendancePunches(
        List<dynamic> attLogs,
        dynamic? gateIn,
        dynamic? gateOut,
        ShiftDefinition matchedShift)
    {
        if (!attLogs.Any())
            return (null, null);

        dynamic? attendIn  = attLogs.First();
        dynamic? attendOut = attLogs.Last();

        // Single punch: first == last
        bool isSinglePunch = attendIn.EventTime == attendOut.EventTime;

        // Rapid cluster: all punches within 10 minutes
        bool isRapidCluster = !isSinglePunch &&
            ((DateTime)attendOut.EventTime - (DateTime)attendIn.EventTime).TotalMinutes < 10;

        if (isRapidCluster || isSinglePunch)
        {
            if (gateOut != null && gateIn == null)
            {
                // Only an exit gate → cluster is an exit event
                return (null, attLogs.Last());
            }

            if (isSinglePunch && gateIn != null && gateOut != null)
            {
                // Single punch with both gates: place it near the closer gate
                double diffToIn  = Math.Abs(((DateTime)attendIn.EventTime - (DateTime)gateIn.EventTime).TotalMinutes);
                double diffToOut = Math.Abs(((DateTime)attendIn.EventTime - (DateTime)gateOut.EventTime).TotalMinutes);
                return diffToOut < diffToIn
                    ? (null, attendIn)   // Closer to exit gate → it's an Out
                    : (attendIn, null);  // Closer to entry gate → it's an In
            }

            // Default: if past mid-shift → exit cluster, else → entry cluster
            var midShift = matchedShift.Start.AddHours(4);
            return (DateTime)attendIn.EventTime < midShift
                ? (attLogs.First(), null)  // Entry cluster
                : (null, attLogs.Last()); // Exit cluster
        }

        return (attendIn, attendOut);
    }

    /// <summary>
    /// Removes gate punches that violate the logical sequence:
    /// Gate In must precede Attend In; Gate Out must follow Attend Out.
    /// </summary>
    public static (dynamic? GateIn, dynamic? GateOut) ValidatePunchSequence(
        dynamic? gateIn,
        dynamic? attendIn,
        dynamic? attendOut,
        dynamic? gateOut)
    {
        // Gate In after Clock In is invalid (employee was already inside)
        if (gateIn != null && attendIn != null && (DateTime)gateIn.EventTime > (DateTime)attendIn.EventTime)
            gateIn = null;

        // Gate Out before Clock Out is invalid (employee hadn't clocked out yet)
        if (gateOut != null && attendOut != null && (DateTime)gateOut.EventTime < (DateTime)attendOut.EventTime)
            gateOut = null;

        return (gateIn, gateOut);
    }

    /// <summary>
    /// Evaluates attendance anomaly codes (B, C1, C2, D1, D2) based on punch times vs shift schedule.
    /// JSGAR employees skip D1/D2 (flexible shifts); they only get B and C2.
    /// </summary>
    public static List<string> EvaluateAttendance(
        dynamic? gateIn,
        dynamic? attendIn,
        dynamic? attendOut,
        dynamic? gateOut,
        ShiftDefinition shift,
        DateTime shiftStartTime,
        bool isJSGAR)
    {
        var evaluations = new List<string>();

        bool hasAnyPunch = gateIn != null || attendIn != null || gateOut != null || attendOut != null;

        if (!hasAnyPunch)
            return evaluations;

        // B: Missing required punches (must have all 4: GateIn, AttendIn, AttendOut, GateOut)
        if (gateIn == null || attendIn == null || gateOut == null || attendOut == null)
            evaluations.Add("B");

        // C1: Gate entry more than 30 minutes before effective shift start
        DateTime effectiveStart = GetEffectiveShiftStart(shift, shiftStartTime);
        if (gateIn != null && (DateTime)gateIn.EventTime < effectiveStart.AddMinutes(-30))
            evaluations.Add("C1");

        // C2: Exit gate > 30 min after attend-out (lingering after clocking out)
        if (gateOut != null && attendOut != null &&
            ((DateTime)gateOut.EventTime - (DateTime)attendOut.EventTime).TotalMinutes > 30)
        {
            evaluations.Add("C2");
        }

        // D1/D2 not applicable for JSGAR (flexible shift department)
        if (!isJSGAR)
        {
            // D1: Late arrival — ALL available arrival logs are after shift start
            bool isGateLate   = gateIn   != null && (DateTime)gateIn.EventTime   > effectiveStart.AddSeconds(2);
            bool isAttendLate = attendIn  != null && (DateTime)attendIn.EventTime > effectiveStart.AddSeconds(2);
            bool hasArrivalLog = gateIn != null || attendIn != null;

            if (hasArrivalLog && (gateIn == null || isGateLate) && (attendIn == null || isAttendLate))
                evaluations.Add("D1");

            // D2: Early departure — ALL available departure logs are before shift end
            bool isGateEarly   = gateOut   != null && (DateTime)gateOut.EventTime   < shift.End.AddSeconds(-2);
            bool isAttendEarly = attendOut  != null && (DateTime)attendOut.EventTime < shift.End.AddSeconds(-2);
            bool hasDepartureLog = gateOut != null || attendOut != null;

            if (hasDepartureLog && (gateOut == null || isGateEarly) && (attendOut == null || isAttendEarly))
                evaluations.Add("D2");
        }

        return evaluations;
    }

    /// <summary>
    /// Formats evaluation codes into the display string, e.g. "[B][C1][D2]".
    /// Returns empty string if no codes.
    /// </summary>
    public static string FormatEvaluation(IEnumerable<string> codes)
    {
        var distinct = codes.Distinct().ToList();
        return distinct.Any() ? "[" + string.Join("][", distinct) + "]" : "";
    }

    /// <summary>
    /// For "Hành chính" shift: if employee starts after 11:00, treat it as a half-day shift starting at 11:30.
    /// </summary>
    private static DateTime GetEffectiveShiftStart(ShiftDefinition shift, DateTime shiftStartTime)
    {
        if (shift.Name == "Hành chính" && shiftStartTime.TimeOfDay > new TimeSpan(11, 0, 0))
            return shiftStartTime.Date.AddHours(11).AddMinutes(30);

        return shift.Start;
    }
}
