using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZkbioDashboard.Models;
using ZkbioDashboard.Services;

namespace ZkbioDashboard.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ExportController : ControllerBase
{
    private readonly ITransactionService _transactionService;

    public ExportController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    // -------------------------------------------------------------------------
    // Transactions export
    // -------------------------------------------------------------------------

    [HttpPost("excel")]
    public async Task<IActionResult> ExportExcel(
        [FromForm] TransactionFilter filter,
        [FromForm] string[] selectedColumns)
    {
        var data = await _transactionService.GetTransactionsForExportAsync(filter);

        using var workbook  = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Transactions");

        WriteTransactionHeaders(worksheet, selectedColumns, styled: false);
        WriteTransactionRows(worksheet, data, selectedColumns, startRow: 2);
        worksheet.Columns().AdjustToContents();

        return ExcelFile(workbook, $"Transactions_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    [HttpPost("attendance/excel")]
    public async Task<IActionResult> ExportAttendanceExcel(
        [FromForm] DateTime date,
        [FromForm] string? pin,
        [FromForm] string? factory,
        [FromForm] string? bu,
        [FromForm] List<string> selectedTypes,
        [FromForm] string[] selectedColumns)
    {
        var allRecords = await _transactionService.GetAttendanceReportAsync(date, pin);
        var filtered   = ApplyAttendanceFilters(allRecords, factory, bu, selectedTypes);

        using var workbook  = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Attendance");

        WriteAttendanceHeaders(worksheet, selectedColumns);
        WriteAttendanceRows(worksheet, filtered, selectedColumns, startRow: 2);

        if (filtered.Count() > 0)
            worksheet.Range(1, 1, 1, selectedColumns.Length).SetAutoFilter();

        worksheet.Columns().AdjustToContents();

        return ExcelFile(workbook, $"Attendance_{date:yyyyMMdd}_{DateTime.Now:HHmmss}.xlsx");
    }

    // -------------------------------------------------------------------------
    // PDF export
    // -------------------------------------------------------------------------

    [HttpPost("pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromForm] TransactionFilter filter,
        [FromForm] string[] selectedColumns)
    {
        var data = await _transactionService.GetTransactionsForExportAsync(filter);

        var document = Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1, Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(8));
            page.Header().Text("Transaction Report").FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
            page.Content().PaddingVertical(10).Table(table =>
            {
                table.ColumnsDefinition(cols => { foreach (var _ in selectedColumns) cols.RelativeColumn(); });
                table.Header(header =>
                {
                    foreach (var col in selectedColumns)
                        header.Cell().Element(PlainHeaderStyle).Text(col).SemiBold();
                });
                foreach (var item in data)
                    foreach (var col in selectedColumns)
                        table.Cell().Element(DataRowStyle).Text(GetTransactionCellValue(item, col)?.ToString() ?? "");
            });
            page.Footer().AlignCenter().Text(x => { x.Span("Page "); x.CurrentPageNumber(); });
        }));

        return PdfFile(document, $"Transactions_{DateTime.Now:yyyyMMddHHmmss}.pdf");
    }

    [HttpPost("attendance/pdf")]
    public async Task<IActionResult> ExportAttendancePdf(
        [FromForm] DateTime date,
        [FromForm] string? pin,
        [FromForm] string? factory,
        [FromForm] string? bu,
        [FromForm] List<string> selectedTypes,
        [FromForm] string[] selectedColumns)
    {
        var allRecords = await _transactionService.GetAttendanceReportAsync(date, pin);
        var filtered   = ApplyAttendanceFilters(allRecords, factory, bu, selectedTypes);

        var document = Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1, Unit.Centimetre);
            page.Header().Text($"Attendance Report - {date:yyyy-MM-dd}").FontSize(16).SemiBold();
            page.Content().PaddingVertical(10).Table(table =>
            {
                table.ColumnsDefinition(cols => { foreach (var _ in selectedColumns) cols.RelativeColumn(); });
                table.Header(header =>
                {
                    foreach (var col in selectedColumns)
                        header.Cell().Background("#215967").PaddingHorizontal(5).PaddingVertical(5)
                              .Text(col).FontColor("#FFFFFF").SemiBold();
                });
                foreach (var item in filtered)
                    foreach (var col in selectedColumns)
                        table.Cell().Element(DataRowStyle)
                             .Text(GetAttendanceCellValue(item, col)?.ToString() ?? "");
            });
        }));

        return PdfFile(document, $"Attendance_{date:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");
    }

    [HttpPost("earlyexit/excel")]
    public async Task<IActionResult> ExportEarlyExitExcel(
        [FromForm] DateTime date,
        [FromForm] string? factory,
        [FromForm] int thresholdMinutes = 5)
    {
        var records = await _transactionService.GetEarlyExitReportAsync(date, thresholdMinutes, factory);

        using var workbook  = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Early Exit");

        var headers = new[] { "Factory", "BU", "Dept Name", "PIN", "Name", "Attend In", "First Gate Out" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.SetValue(headers[i]);
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#215967");
        }

        int row = 2;
        foreach (var item in records)
        {
            worksheet.Cell(row, 1).SetValue(item.Factory == "JSG" ? "JIAHSIN" : item.Factory);
            worksheet.Cell(row, 2).SetValue(item.BU);
            worksheet.Cell(row, 3).SetValue(item.DeptName);
            worksheet.Cell(row, 4).SetValue(item.Pin);
            worksheet.Cell(row, 5).SetValue(item.FullName);
            worksheet.Cell(row, 6).SetValue(item.AttendIn?.ToString("HH:mm:ss") ?? "");
            worksheet.Cell(row, 7).SetValue(item.FirstGateOut?.ToString("HH:mm:ss") ?? "");
            row++;
        }

        if (records.Any())
            worksheet.Range(1, 1, 1, headers.Length).SetAutoFilter();

        worksheet.Columns().AdjustToContents();

        return ExcelFile(workbook, $"EarlyExit_{date:yyyyMMdd}_{DateTime.Now:HHmmss}.xlsx");
    }

    [HttpPost("earlyexit/pdf")]
    public async Task<IActionResult> ExportEarlyExitPdf(
        [FromForm] DateTime date,
        [FromForm] string? factory,
        [FromForm] int thresholdMinutes = 5)
    {
        var records = await _transactionService.GetEarlyExitReportAsync(date, thresholdMinutes, factory);
        var headers = new[] { "Factory", "BU", "Dept Name", "PIN", "Name", "Attend In", "First Gate Out" };

        var document = Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1, Unit.Centimetre);
            page.Header().Text($"Early Exit Report - {date:yyyy-MM-dd} (≤ {thresholdMinutes} min)")
                .FontSize(16).SemiBold();
            page.Content().PaddingVertical(10).Table(table =>
            {
                table.ColumnsDefinition(cols => { foreach (var _ in headers) cols.RelativeColumn(); });
                table.Header(header =>
                {
                    foreach (var col in headers)
                        header.Cell().Background("#215967").PaddingHorizontal(5).PaddingVertical(5)
                              .Text(col).FontColor("#FFFFFF").SemiBold();
                });
                foreach (var item in records)
                {
                    var displayFactory = (item.Factory == "JSG" ? "JIAHSIN" : item.Factory);
                    var cells = new[] { displayFactory, item.BU, item.DeptName, item.Pin, item.FullName,
                        item.AttendIn?.ToString("HH:mm:ss") ?? "", item.FirstGateOut?.ToString("HH:mm:ss") ?? "" };
                    foreach (var val in cells)
                        table.Cell().Element(DataRowStyle).Text(val);
                }
            });
        }));

        return PdfFile(document, $"EarlyExit_{date:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");
    }


    // -------------------------------------------------------------------------
    // Shared filter logic
    // -------------------------------------------------------------------------

    /// <summary>
    /// Filters attendance records by factory, BU, and evaluation type codes.
    /// Only records with a non-empty evaluation are included.
    /// </summary>
    private static IEnumerable<AttendanceRecord> ApplyAttendanceFilters(
        IEnumerable<AttendanceRecord> records,
        string? factory,
        string? bu,
        IEnumerable<string>? selectedTypes)
    {
        var filtered = records.Where(r => !string.IsNullOrEmpty(r.Evaluation));

        if (!string.IsNullOrEmpty(factory))
            filtered = filtered.Where(r => IsAttendanceFactoryMatch(r, factory));

        if (!string.IsNullOrEmpty(bu))
            filtered = filtered.Where(r => IsAttendanceBUMatch(r, factory, bu));

        var typeList = selectedTypes?.ToList();
        if (typeList?.Count > 0)
        {
            filtered = filtered.Where(r =>
                r.Evaluation
                    .Split([',', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Any(code => typeList.Contains(code)));
        }

        return filtered;
    }

    private static bool IsAttendanceFactoryMatch(AttendanceRecord record, string selectedFactory)
    {
        return selectedFactory switch
        {
            "JIAHSIN" => record.Factory == "JIAHSIN" ||
                         (record.Factory == "JSG" && record.FactoryCluster == "JIAHSIN"),
            "SHIMMER" => record.Factory == "SHIMMER" ||
                         (record.Factory == "JSG" && record.FactoryCluster == "SHIMMER"),
            _ => record.Factory == selectedFactory
        };
    }

    private static bool IsAttendanceBUMatch(AttendanceRecord record, string? selectedFactory, string selectedBU)
    {
        if (selectedBU == "JSG" && selectedFactory == "JIAHSIN")
            return record.BU == "JSG" && record.FactoryCluster == "JIAHSIN";

        if (selectedBU == "JSG" && selectedFactory == "SHIMMER")
            return record.BU == "JSG" && record.FactoryCluster == "SHIMMER";

        return record.BU == selectedBU;
    }

    // -------------------------------------------------------------------------
    // Cell value resolvers
    // -------------------------------------------------------------------------

    private static object? GetTransactionCellValue(AccTransaction item, string columnName) => columnName switch
    {
        "Time"         => item.EventTime.ToString("yyyy-MM-dd HH:mm:ss"),
        "Area Name"    => item.AreaName,
        "Dept Name"    => item.DeptName,
        "Dept Code"    => item.DeptCode,
        "PIN"          => item.Pin,
        "Name"         => item.FullName,
        "Device Alias" => item.DevAlias,
        "Event Point"  => item.EventPointName,
        "Status"       => item.Status,
        "Verify Mode"  => item.VerifyModeDisplay,
        "Event No"     => item.EventNo,
        _              => ""
    };

    private static object? GetAttendanceCellValue(AttendanceRecord item, string columnName) => columnName switch
    {
        "Shift Date"    => item.Date.ToString("yyyy-MM-dd"),
        "Factory"       => item.Factory,
        "BU"            => item.BU,
        "Dept Name"     => item.DeptName,
        "PIN"           => item.Pin,
        "Name"          => item.FullName,
        "Gate In"       => item.GateIn?.ToString("HH:mm:ss") ?? "",
        "Attend In"     => item.AttendIn?.ToString("HH:mm:ss") ?? "",
        "Attend Out"    => item.AttendOut?.ToString("HH:mm:ss") ?? "",
        "Gate Out (ACS)"=> item.GateOut?.ToString("HH:mm:ss") ?? "",
        "Type"          => item.Evaluation,
        _               => ""
    };

    // -------------------------------------------------------------------------
    // Excel write helpers
    // -------------------------------------------------------------------------

    private static void WriteTransactionHeaders(IXLWorksheet ws, string[] columns, bool styled)
    {
        for (int i = 0; i < columns.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.SetValue(columns[i]);
            cell.Style.Font.Bold = true;
        }
    }

    private static void WriteAttendanceHeaders(IXLWorksheet ws, string[] columns)
    {
        for (int i = 0; i < columns.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.SetValue(columns[i]);
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#215967");
        }
    }

    private static void WriteTransactionRows(IXLWorksheet ws, IEnumerable<AccTransaction> data, string[] columns, int startRow)
    {
        int row = startRow;
        foreach (var item in data)
        {
            for (int col = 0; col < columns.Length; col++)
                ws.Cell(row, col + 1).SetValue(XLCellValue.FromObject(GetTransactionCellValue(item, columns[col])));
            row++;
        }
    }

    private static void WriteAttendanceRows(IXLWorksheet ws, IEnumerable<AttendanceRecord> data, string[] columns, int startRow)
    {
        int row = startRow;
        foreach (var item in data)
        {
            for (int col = 0; col < columns.Length; col++)
                ws.Cell(row, col + 1).SetValue(XLCellValue.FromObject(GetAttendanceCellValue(item, columns[col])));
            row++;
        }
    }

    // -------------------------------------------------------------------------
    // PDF style helpers
    // -------------------------------------------------------------------------

    private static IContainer PlainHeaderStyle(IContainer container) =>
        container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);

    private static IContainer DataRowStyle(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2);

    // -------------------------------------------------------------------------
    // File result helpers
    // -------------------------------------------------------------------------

    private IActionResult ExcelFile(XLWorkbook workbook, string fileName)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private IActionResult PdfFile(Document document, string fileName)
    {
        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return File(stream.ToArray(), "application/pdf", fileName);
    }
}
