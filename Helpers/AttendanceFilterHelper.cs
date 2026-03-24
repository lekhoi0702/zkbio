using System;
using System.Collections.Generic;
using System.Linq;
using ZkbioDashboard.Models;

namespace ZkbioDashboard.Helpers;

public static class AttendanceFilterHelper
{
    private static readonly TimeSpan ThirtyMinutes = TimeSpan.FromMinutes(30);

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

    // -------------------------------------------------------------------------
    // Filtering / extraction helpers
    // -------------------------------------------------------------------------

    public static IEnumerable<AttendanceRecord> ApplyFilters(
        IEnumerable<AttendanceRecord> records,
        string? factory,
        string? bu)
    {
        // Include any record that has at least one effective code
        var filtered = records.Where(r => ComputeEffectiveCodes(r).Any());

        if (!string.IsNullOrEmpty(factory))
            filtered = filtered.Where(r => IsFactoryMatch(r, factory));

        if (!string.IsNullOrEmpty(bu))
            filtered = filtered.Where(r => IsBUMatch(r, factory, bu));

        return filtered;
    }

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

    private static IEnumerable<string> ParseEvaluationString(string? evaluation)
    {
        return string.IsNullOrEmpty(evaluation)
            ? Enumerable.Empty<string>()
            : evaluation
                .Split([',', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
                .Select(code => code.Trim());
    }
}
