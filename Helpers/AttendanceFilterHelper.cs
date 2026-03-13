using System;
using System.Collections.Generic;
using System.Linq;
using ZkbioDashboard.Models;

namespace ZkbioDashboard.Helpers;

public static class AttendanceFilterHelper
{
    private static readonly TimeSpan ThirtyMinutes = TimeSpan.FromMinutes(30);

    private static readonly IReadOnlyDictionary<string, string> TypeLabels = new Dictionary<string, string>
    {
        ["B"]  = "[B] Missing the required 2+2 clock-in/clock-out records",
        ["C1"] = "[C1] Gate Entry 30 Minutes Early",
        ["C2"] = "[C2] Gate Exit > 30 minutes after Attend Out",
        ["D1"] = "[D1] Late Arrival",
        ["D2"] = "[D2] Early Departure"
    };

    private static readonly IReadOnlyDictionary<string, string> ContractorTypeLabels = new Dictionary<string, string>
    {
        ["B"]  = "[B] Missing required 2+2 records",
        ["C1"] = "[C1] Gate Entry 30m Early",
        ["C2"] = "[C2] Gate Exit > 30m after Attend Out",
        ["D1"] = "[D1] Late Arrival",
        ["D2"] = "[D2] Early Departure"
    };

    // -------------------------------------------------------------------------
    // Core logic — single source of truth for all type code evaluation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Computes effective exception codes for a record using live field data:
    /// B  — any of GateIn/AttendIn/AttendOut/GateOut is null
    /// C1 — GateIn earlier than AttendIn by more than 30 min
    /// C2 — |AttendOut - GateOut| > 30 min
    /// D1/D2 — still read from the pre-computed Evaluation string
    /// </summary>
    public static IEnumerable<string> ComputeEffectiveCodes(AttendanceRecord record)
    {
        var codes = new List<string>();

        // B: at least one field is missing → incomplete records
        bool anyMissing = !record.GateIn.HasValue || !record.AttendIn.HasValue ||
                          !record.AttendOut.HasValue || !record.GateOut.HasValue;

        if (anyMissing)
            codes.Add("B");

        // C1: GateIn earlier than AttendIn by more than 30 minutes
        if (record.AttendIn.HasValue && record.GateIn.HasValue &&
            record.GateIn.Value < record.AttendIn.Value.AddMinutes(-30))
        {
            codes.Add("C1");
        }

        // C2: gap between AttendOut and GateOut > 30 min
        if (record.AttendOut.HasValue && record.GateOut.HasValue &&
            (record.AttendOut.Value - record.GateOut.Value).Duration() > ThirtyMinutes)
        {
            codes.Add("C2");
        }

        // D1, D2: from pre-computed Evaluation string (source system)
        foreach (var code in ParseEvaluationString(record.Evaluation)
                     .Where(c => c is "D1" or "D2"))
        {
            codes.Add(code);
        }

        return codes;
    }

    /// <summary>
    /// Formats the effective type codes as a bracketed string, e.g. "[B][C1][D2]"
    /// </summary>
    public static string FormatEffectiveType(AttendanceRecord record)
    {
        var codes = ComputeEffectiveCodes(record).ToList();
        return codes.Count == 0 ? string.Empty : string.Concat(codes.Select(c => $"[{c}]"));
    }

    // -------------------------------------------------------------------------
    // Filtering / extraction helpers
    // -------------------------------------------------------------------------

    public static IEnumerable<AttendanceRecord> ApplyFilters(
        IEnumerable<AttendanceRecord> records,
        string? factory,
        string? bu,
        IEnumerable<string>? selectedTypes)
    {
        // Include any record that has at least one effective code
        var filtered = records.Where(r => ComputeEffectiveCodes(r).Any());

        if (!string.IsNullOrEmpty(factory))
            filtered = filtered.Where(r => IsFactoryMatch(r, factory));

        if (!string.IsNullOrEmpty(bu))
            filtered = filtered.Where(r => IsBUMatch(r, factory, bu));

        filtered = ApplyTypeFilter(filtered, selectedTypes);

        return filtered;
    }

    public static IEnumerable<AttendanceRecord> ApplyTypeFilter(
        IEnumerable<AttendanceRecord> records,
        IEnumerable<string>? selectedTypes)
    {
        var typeList = selectedTypes?.ToList();
        if (typeList?.Count > 0)
        {
            return records.Where(r =>
                ComputeEffectiveCodes(r).Any(code => typeList.Contains(code)));
        }

        return records;
    }

    public static List<string> ExtractTypeCodes(IEnumerable<AttendanceRecord> records)
    {
        return records
            .SelectMany(ComputeEffectiveCodes)
            .Distinct()
            .OrderBy(t => t)
            .ToList();
    }

    /// <summary>Returns effective codes for a single record (alias for ComputeEffectiveCodes).</summary>
    public static IEnumerable<string> GetTypeCodes(AttendanceRecord record) =>
        ComputeEffectiveCodes(record);

    // -------------------------------------------------------------------------
    // Label helpers
    // -------------------------------------------------------------------------

    public static string GetTypeLabel(string type) =>
        TypeLabels.TryGetValue(type, out var label) ? label : type;

    public static string GetContractorTypeLabel(string type) =>
        ContractorTypeLabels.TryGetValue(type, out var label) ? label : type;

    // -------------------------------------------------------------------------
    // Factory / BU match helpers
    // -------------------------------------------------------------------------

    public static bool IsFactoryMatch(AttendanceRecord record, string selectedFactory)
    {
        return IsFactoryMatch(record.Factory, record.FactoryCluster, selectedFactory);
    }

    public static bool IsFactoryMatch(string factory, string? factoryCluster, string selectedFactory)
    {
        return selectedFactory switch
        {
            "JIAHSIN" => factory == "JIAHSIN" ||
                         (factory == "JSG" && factoryCluster == "JIAHSIN"),
            _ => factory == selectedFactory
        };
    }

    public static bool IsBUMatch(AttendanceRecord record, string? selectedFactory, string selectedBU)
    {
        return IsBUMatch(record.BU, record.FactoryCluster, selectedFactory, selectedBU);
    }

    public static bool IsBUMatch(string bu, string? factoryCluster, string? selectedFactory, string selectedBU)
    {
        if (selectedBU == "JSG" && selectedFactory == "JIAHSIN")
            return bu == "JSG" && factoryCluster == "JIAHSIN";

        return bu == selectedBU;
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private static IEnumerable<string> ParseEvaluationString(string? evaluation)
    {
        return string.IsNullOrEmpty(evaluation)
            ? Enumerable.Empty<string>()
            : evaluation
                .Split([',', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
                .Select(code => code.Trim());
    }
}
