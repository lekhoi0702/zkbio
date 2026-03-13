(function (window, $) {
    if (!$) return;

    function initSelectAllCheckbox(selectAllSelector, itemSelector) {
        var $selectAll = $(selectAllSelector);
        if ($selectAll.length === 0) return;

        $selectAll.on("change", function () {
            $(itemSelector).prop("checked", this.checked);
        });

        $(document).on("change", itemSelector, function () {
            var total = $(itemSelector).length;
            var checked = $(itemSelector + ":checked").length;
            $selectAll.prop("checked", total > 0 && total === checked);
        });
    }

    function initSimpleDropdown(buttonSelector, menuSelector) {
        var $btn = $(buttonSelector);
        var $menu = $(menuSelector);
        if ($btn.length === 0 || $menu.length === 0) return;

        $btn.on("click", function (e) {
            e.stopPropagation();
            $menu.toggleClass("open");
        });

        $menu.on("click", function (e) {
            e.stopPropagation();
        });

        $(document).on("click", function () {
            $menu.removeClass("open");
        });
    }

    function initTypeMultiSelect(options) {
        var cfg = options || {};
        var buttonSelector = cfg.buttonSelector;
        var menuSelector = cfg.menuSelector;
        var checkboxSelector = cfg.checkboxSelector || "[name='SelectedTypes']";
        var emptyText = cfg.emptyText || "-- All --";
        var labelBuilder = cfg.labelBuilder || function ($checkbox) {
            return $checkbox.next("label").text();
        };

        var $btn = $(buttonSelector);
        var $menu = $(menuSelector);
        if ($btn.length === 0 || $menu.length === 0) return;

        $btn.on("click", function (e) {
            e.stopPropagation();
            $menu.toggleClass("open");
        });

        $menu.on("click", function (e) {
            e.stopPropagation();
        });

        $(document).on("click", function () {
            $menu.removeClass("open");
        });

        function syncLabel() {
            var selected = [];
            $(checkboxSelector + ":checked").each(function () {
                selected.push(labelBuilder($(this)));
            });
            $btn.text(selected.length > 0 ? selected.join(", ") : emptyText);
        }

        $(document).on("change", checkboxSelector, syncLabel);
        syncLabel();
    }

    function openTransactionDetails(options) {
        var cfg = options || {};
        var employeeHeader = cfg.employeeHeader || "";
        var dateHeader = cfg.dateHeader || "";
        var url = cfg.url || "?handler=Details";
        var requestData = cfg.requestData || {};
        var noRecordsText = cfg.noRecordsText || "No records found.";
        var errorText = cfg.errorText || "Error fetching details.";

        $("#modalEmployeeHeader").text(employeeHeader);
        $("#modalDateHeader").text(dateHeader);
        $("#detailsTableBody").empty();
        $("#detailsTable").hide();
        $("#loadingIndicator").show();
        $("#detailsModal").modal("show");

        $.ajax({
            url: url,
            type: "GET",
            data: requestData,
            success: function (data) {
                $("#loadingIndicator").hide();
                if (data && data.length > 0) {
                    if (typeof window.renderTransactionRows === "function") {
                        window.renderTransactionRows(data, "#detailsTableBody");
                    } else if (typeof window.buildTransactionRow === "function") {
                        data.forEach(function (log) { $("#detailsTableBody").append(window.buildTransactionRow(log)); });
                    }
                } else {
                    $("#detailsTableBody").append('<tr><td colspan="7" class="text-center">' + noRecordsText + "</td></tr>");
                }
                $("#detailsTable").show();
            },
            error: function () {
                $("#loadingIndicator").hide();
                alert(errorText);
            }
        });
    }

    function initDatePresetRange(options) {
        var cfg = options || {};
        var presetSelector = cfg.presetSelector || ".at-preset";
        var fromSelector = cfg.fromSelector;
        var toSelector = cfg.toSelector;
        var formSelector = cfg.formSelector;
        var submitDelayMs = cfg.submitDelayMs || 80;
        if (!fromSelector || !toSelector || !formSelector) return;

        function fmtDate(d) {
            var pad = function (n) { return String(n).padStart(2, "0"); };
            return d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate());
        }

        function setDateRange(fromDate, toDate) {
            var fromInput = $(fromSelector)[0];
            var toInput = $(toSelector)[0];

            if (fromInput && fromInput._flatpickr) {
                fromInput._flatpickr.setDate(fromDate + " 00:00:00", true);
            } else {
                $(fromSelector).val(fromDate + " 00:00:00");
            }

            if (toInput && toInput._flatpickr) {
                toInput._flatpickr.setDate(toDate + " 23:59:59", true);
            } else {
                $(toSelector).val(toDate + " 23:59:59");
            }
        }

        $(document).on("click", presetSelector, function () {
            $(presetSelector).removeClass("active");
            $(this).addClass("active");

            var now = new Date();
            var today = fmtDate(now);
            var preset = $(this).data("preset");

            if (preset === "today") {
                setDateRange(today, today);
            } else if (preset === "yesterday") {
                var y = new Date(now);
                y.setDate(y.getDate() - 1);
                var yd = fmtDate(y);
                setDateRange(yd, yd);
            } else if (preset === "7days") {
                var d7 = new Date(now);
                d7.setDate(d7.getDate() - 6);
                setDateRange(fmtDate(d7), today);
            } else if (preset === "thismonth") {
                var fm = new Date(now.getFullYear(), now.getMonth(), 1);
                setDateRange(fmtDate(fm), today);
            } else if (preset === "lastmonth") {
                var lm1 = new Date(now.getFullYear(), now.getMonth() - 1, 1);
                var lm2 = new Date(now.getFullYear(), now.getMonth(), 0);
                setDateRange(fmtDate(lm1), fmtDate(lm2));
            } else {
                return;
            }

            setTimeout(function () {
                $(formSelector).submit();
            }, submitDelayMs);
        });
    }

    window.ZKBioUi = {
        initSelectAllCheckbox: initSelectAllCheckbox,
        initSimpleDropdown: initSimpleDropdown,
        initTypeMultiSelect: initTypeMultiSelect,
        openTransactionDetails: openTransactionDetails,
        initDatePresetRange: initDatePresetRange
    };
})(window, window.jQuery);
