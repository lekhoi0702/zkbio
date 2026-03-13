using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZkbioDashboard.Helpers;
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
        worksheet.Style.Font.FontSize = 12;
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
        var filtered   = AttendanceFilterHelper.ApplyFilters(allRecords, factory, bu, selectedTypes);

        using var workbook  = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Attendance");

        WriteAttendanceHeaders(worksheet, selectedColumns);
        WriteAttendanceRows(worksheet, filtered, selectedColumns, startRow: 2);
        worksheet.Style.Font.FontSize = 12;

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
        var columns = selectedColumns.ToList();
        var generatedAt = DateTime.Now;

        var document = Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(0.7f, Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(6.5f));
            page.Header().PaddingBottom(6).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Transaction Report").FontSize(12).SemiBold().FontColor("#0f172a");
                    col.Item().Text($"Generated: {generatedAt:yyyy-MM-dd HH:mm}").FontSize(7).FontColor("#475569");
                });
                row.ConstantItem(220).AlignRight().Column(col =>
                {
                    col.Item().Text($"From: {filter.FromDate:yyyy-MM-dd HH:mm}").FontSize(7);
                    col.Item().Text($"To: {filter.ToDate:yyyy-MM-dd HH:mm}").FontSize(7);
                });
            });
            page.Content().PaddingVertical(6).Table(table =>
            {
                table.ColumnsDefinition(cols => DefinePdfColumns(cols, columns));
                table.Header(header =>
                {
                    foreach (var col in columns)
                        header.Cell().Element(PdfHeaderCell).Text(GetPdfHeaderLabel(col));
                });
                var rowIndex = 0;
                foreach (var item in data)
                {
                    var shaded = rowIndex % 2 == 1;
                    foreach (var col in columns)
                        table.Cell().Element(c => PdfDataCell(c, shaded)).Text(GetTransactionCellValue(item, col)?.ToString() ?? "");
                    rowIndex++;
                }
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
        var filtered   = AttendanceFilterHelper.ApplyFilters(allRecords, factory, bu, selectedTypes);
        var columns = selectedColumns.ToList();
        var generatedAt = DateTime.Now;

        var document = Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(0.7f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(6.5f));
            page.Header().PaddingBottom(6).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Attendance Report").FontSize(12).SemiBold().FontColor("#0f172a");
                    col.Item().Text($"Date: {date:yyyy-MM-dd}").FontSize(7).FontColor("#475569");
                });
                row.ConstantItem(220).AlignRight().Column(col =>
                {
                    col.Item().Text($"Factory: {factory ?? "All"}").FontSize(7);
                    col.Item().Text($"BU: {bu ?? "All"}").FontSize(7);
                    col.Item().Text($"Generated: {generatedAt:yyyy-MM-dd HH:mm}").FontSize(7);
                });
            });
            page.Content().PaddingVertical(6).Table(table =>
            {
                table.ColumnsDefinition(cols => DefinePdfColumns(cols, columns));
                table.Header(header =>
                {
                    foreach (var col in columns)
                        header.Cell().Element(PdfHeaderCell).Text(GetPdfHeaderLabel(col));
                });
                var rowIndex = 0;
                foreach (var item in filtered)
                {
                    var shaded = rowIndex % 2 == 1;
                    foreach (var col in columns)
                        table.Cell().Element(c => PdfDataCell(c, shaded))
                             .Text(GetAttendanceCellValue(item, col)?.ToString() ?? "");
                    rowIndex++;
                }
            });
        }));

        return PdfFile(document, $"Attendance_{date:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");
    }

    [HttpPost("earlyexit/excel")]
    public async Task<IActionResult> ExportEarlyExitExcel(
        [FromForm] DateTime date,
        [FromForm] string? factory,
        [FromForm] string? bu,
        [FromForm] int thresholdMinutes = 1)
    {
        var records = await _transactionService.GetEarlyExitReportAsync(date, thresholdMinutes, factory);
        if (!string.IsNullOrEmpty(bu))
            records = records.Where(r => r.BU == bu);

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
        worksheet.Style.Font.FontSize = 12;

        int row = 2;
        foreach (var item in records)
        {
            worksheet.Cell(row, 1).SetValue(item.Factory);
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
        [FromForm] string? bu,
        [FromForm] int thresholdMinutes = 1)
    {
        var records = await _transactionService.GetEarlyExitReportAsync(date, thresholdMinutes, factory);
        if (!string.IsNullOrEmpty(bu))
            records = records.Where(r => r.BU == bu);
        var headers = new[] { "Factory", "BU", "Dept Name", "PIN", "Name", "Attend In", "First Gate Out" };
        var generatedAt = DateTime.Now;

        var document = Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(0.7f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(6.5f));
            page.Header().PaddingBottom(6).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Early Exit Report").FontSize(12).SemiBold().FontColor("#0f172a");
                    col.Item().Text($"Date: {date:yyyy-MM-dd} (<= {thresholdMinutes} min)")
                        .FontSize(7).FontColor("#475569");
                });
                row.ConstantItem(220).AlignRight().Column(col =>
                {
                    col.Item().Text($"Factory: {factory ?? "All"}").FontSize(7);
                    col.Item().Text($"BU: {bu ?? "All"}").FontSize(7);
                    col.Item().Text($"Generated: {generatedAt:yyyy-MM-dd HH:mm}").FontSize(7);
                });
            });
            page.Content().PaddingVertical(6).Table(table =>
            {
                table.ColumnsDefinition(cols => DefinePdfColumns(cols, headers));
                table.Header(header =>
                {
                    foreach (var col in headers)
                        header.Cell().Element(PdfHeaderCell).Text(GetPdfHeaderLabel(col));
                });
                var rowIndex = 0;
                foreach (var item in records)
                {
                    var shaded = rowIndex % 2 == 1;
                    var cells = new[] { item.Factory, item.BU, item.DeptName, item.Pin, item.FullName,
                        item.AttendIn?.ToString("HH:mm:ss") ?? "", item.FirstGateOut?.ToString("HH:mm:ss") ?? "" };
                    foreach (var val in cells)
                        table.Cell().Element(c => PdfDataCell(c, shaded)).Text(val);
                    rowIndex++;
                }
            });
        }));

        return PdfFile(document, $"EarlyExit_{date:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");
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
        "Type"          => AttendanceFilterHelper.FormatEffectiveType(item),
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

    private static void DefinePdfColumns(TableColumnsDefinitionDescriptor cols, IReadOnlyList<string> columns)
    {
        foreach (var col in columns)
        {
            switch (col)
            {
                case "PIN":
                    cols.ConstantColumn(48);
                    break;
                case "BU":
                case "Type":
                    cols.ConstantColumn(42);
                    break;
                case "Status":
                    cols.ConstantColumn(50);
                    break;
                case "Verify Mode":
                    cols.ConstantColumn(60);
                    break;
                case "Time":
                    cols.ConstantColumn(78);
                    break;
                case "Shift Date":
                    cols.ConstantColumn(58);
                    break;
                case "Gate In":
                case "Attend In":
                case "Attend Out":
                case "Gate Out (ACS)":
                case "First Gate Out":
                    cols.ConstantColumn(56);
                    break;
                case "Dept Name":
                    cols.RelativeColumn(2.4f);
                    break;
                case "Name":
                    cols.RelativeColumn(2.1f);
                    break;
                case "Event Point Name":
                case "Event Point":
                    cols.RelativeColumn(2.2f);
                    break;
                case "Device Alias":
                case "Device":
                    cols.RelativeColumn(1.6f);
                    break;
                case "Factory":
                case "Area Name":
                    cols.RelativeColumn(1.3f);
                    break;
                default:
                    cols.RelativeColumn(1.2f);
                    break;
            }
        }
    }

    private static string GetPdfHeaderLabel(string columnName) => columnName switch
    {
        "Area Name"       => "Factory",
        "Dept Name"       => "Dept",
        "Event Point Name"=> "Event Point",
        "Device Alias"    => "Device",
        "Verify Mode"     => "Verify",
        "Gate Out (ACS)"  => "Gate Out",
        "Shift Date"      => "Shift",
        _                 => columnName
    };

    private static IContainer PdfHeaderCell(IContainer container) =>
        container
            .Background("#0f172a")
            .PaddingHorizontal(2)
            .PaddingVertical(3)
            .DefaultTextStyle(x => x.FontSize(6.5f).FontColor(Colors.White).SemiBold());

    private static IContainer PdfDataCell(IContainer container, bool shaded) =>
        container
            .Background(shaded ? "#f5f8fb" : Colors.White)
            .BorderBottom(1).BorderColor(Colors.Grey.Lighten3)
            .PaddingHorizontal(2)
            .PaddingVertical(1);

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

