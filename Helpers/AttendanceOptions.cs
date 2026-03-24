using System.Collections.Generic;

namespace ZkbioDashboard.Helpers;

public static class AttendanceOptions
{
    public static readonly IReadOnlyList<string> Factories = new[]
    {
        "JIAHSIN",
        "JT1",
        "JT2",
        "PD1",
        "SHIMMER"
    };

    public static readonly IReadOnlyList<string> BUs = new[]
    {
        "JSG",
        "BU1",
        "BU2",
        "BU3",
        "PD1",
        "SHIMMER",
        "JT1",
        "JT2"
    };

}

