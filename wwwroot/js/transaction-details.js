(function (window, $) {
    if (!$) return;

    function getShiftSortKey(dateValue) {
        var d = new Date(dateValue);
        if (Number.isNaN(d.getTime())) return Number.MAX_SAFE_INTEGER;

        var hour = d.getHours();
        var minute = d.getMinutes();
        var second = d.getSeconds();
        var timeInSeconds = (hour * 3600) + (minute * 60) + second;

        // Night shift display order: 22:00 -> 23:59 -> 00:00 -> 05:59
        return hour < 6 ? timeInSeconds + (24 * 3600) : timeInSeconds;
    }

    function sortTransactionsForDisplay(logs) {
        if (!Array.isArray(logs)) return [];
        if (logs.length <= 1) return logs.slice();

        var hasLateNight = logs.some(function (x) {
            var d = new Date(x.eventTime);
            return !Number.isNaN(d.getTime()) && d.getHours() >= 22;
        });
        var hasEarlyMorning = logs.some(function (x) {
            var d = new Date(x.eventTime);
            return !Number.isNaN(d.getTime()) && d.getHours() < 6;
        });

        var sorted = logs.slice();
        if (hasLateNight && hasEarlyMorning) {
            sorted.sort(function (a, b) {
                return getShiftSortKey(a.eventTime) - getShiftSortKey(b.eventTime);
            });
            return sorted;
        }

        sorted.sort(function (a, b) {
            return new Date(a.eventTime).getTime() - new Date(b.eventTime).getTime();
        });
        return sorted;
    }

    function buildTransactionRow(log) {
        var d = new Date(log.eventTime);
        var pad = function (n) { return n.toString().padStart(2, "0"); };
        var formattedTime = d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate()) + " "
            + pad(d.getHours()) + ":" + pad(d.getMinutes()) + ":" + pad(d.getSeconds());

        var statusBadge = log.status === "Normal" ? "txd-pill txd-pill-ok"
            : log.status === "Exception" ? "txd-pill txd-pill-bad"
                : "txd-pill txd-pill-mute";

        var typeBadge = log.type === "GATE IN" ? "txd-pill txd-pill-info"
            : log.type === "GATE OUT" ? "txd-pill txd-pill-warn"
                : log.type === "ATTENDANCE" ? "txd-pill txd-pill-ok"
                    : log.type === "VERIFY" ? "txd-pill txd-pill-mute"
                        : "txd-pill txd-pill-info";

        return "<tr>"
            + "<td>" + formattedTime + "</td>"
            + "<td>" + (log.areaName || "-") + "</td>"
            + "<td><span class=\"" + typeBadge + "\">" + (log.type || "Other") + "</span></td>"
            + "<td>" + (log.devAlias || "-") + "</td>"
            + "<td>" + (log.eventPointName || "-") + "</td>"
            + "<td>" + (log.verifyModeDisplay || "-") + "</td>"
            + "<td><span class=\"" + statusBadge + "\">" + (log.status || "-") + "</span></td>"
            + "</tr>";
    }

    function renderTransactionRows(logs, tableBodySelector) {
        var ordered = sortTransactionsForDisplay(logs);
        ordered.forEach(function (log) {
            $(tableBodySelector).append(buildTransactionRow(log));
        });
    }

    window.buildTransactionRow = buildTransactionRow;
    window.renderTransactionRows = renderTransactionRows;
})(window, window.jQuery);
