using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ZkbioDashboard.Helpers;
using ZkbioDashboard.Models;
using ZkbioDashboard.Services;

namespace ZkbioDashboard.Pages;

public class DeviceModel : PageModel
{
    private readonly ITransactionService _transactionService;
    private readonly IDeviceStatusService _deviceStatusService;

    public DeviceModel(ITransactionService transactionService, IDeviceStatusService deviceStatusService)
    {
        _transactionService = transactionService;
        _deviceStatusService = deviceStatusService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Factory { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DeviceType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DevAlias { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SN { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AreaName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? IpAddress { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DeviceName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? IsRegistrationDevice { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FwVersion { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
    public int TotalCount { get; private set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public List<string> Factories { get; private set; } = [];

    public IReadOnlyList<DeviceRecord> Records { get; private set; } = Array.Empty<DeviceRecord>();

    public async Task OnGetAsync()
    {
        ViewData["StartLoading"] = true;
        Factories = AttendanceOptions.Factories.ToList();
        var result = await LoadPageDataAsync();
        TotalCount = result.TotalCount;
        Records = result.Records;
    }

    public async Task<JsonResult> OnGetDataAsync()
    {
        Factories = AttendanceOptions.Factories.ToList();
        var result = await LoadPageDataAsync();
        TotalCount = result.TotalCount;

        return new JsonResult(new
        {
            data = result.Records.Select(d => new
            {
                d.DevAlias,
                d.SN,
                d.AreaName,
                d.IpAddress,
                d.Status,
                d.DeviceName,
                d.IsRegistrationDevice,
                d.FwVersion
            }),
            totalCount = TotalCount,
            pageNumber = PageNumber,
            totalPages = TotalPages
        });
    }

    private async Task<(List<DeviceRecord> Records, int TotalCount)> LoadPageDataAsync()
    {
        var data = (await _transactionService.GetDevicesAsync(Factory)).ToList();
        var filtered = ApplyFilters(data);

        if (!string.IsNullOrWhiteSpace(Status))
        {
            var withStatus = await _deviceStatusService.ApplyStatusAsync(filtered);
            var statusFiltered = ApplyStatusFilter(withStatus);
            var page = statusFiltered
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();
            return (page, statusFiltered.Count);
        }

        var paged = filtered
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
        var withPageStatus = await _deviceStatusService.ApplyStatusAsync(paged);
        return (withPageStatus, filtered.Count);
    }

    private List<DeviceRecord> ApplyFilters(IEnumerable<DeviceRecord> records)
    {
        var query = records;

        if (!string.IsNullOrWhiteSpace(DeviceType))
        {
            var type = DeviceType.Trim();
            if (type.Equals("ATT", StringComparison.OrdinalIgnoreCase))
                query = query.Where(d => d.DevAlias.Contains("ATT", StringComparison.OrdinalIgnoreCase));
            else if (type.Equals("ACS", StringComparison.OrdinalIgnoreCase))
                query = query.Where(d => d.DevAlias.Contains("ACS", StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(DevAlias))
            query = query.Where(d => d.DevAlias.Contains(DevAlias.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(SN))
            query = query.Where(d => d.SN.Contains(SN.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(AreaName))
            query = query.Where(d => d.AreaName.Contains(AreaName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(IpAddress))
            query = query.Where(d => d.IpAddress.Contains(IpAddress.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(DeviceName))
            query = query.Where(d => d.DeviceName.Contains(DeviceName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(IsRegistrationDevice))
        {
            var flag = IsRegistrationDevice.Trim();
            if (flag.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                query = query.Where(d => d.IsRegistrationDevice);
            else if (flag.Equals("No", StringComparison.OrdinalIgnoreCase))
                query = query.Where(d => !d.IsRegistrationDevice);
        }

        if (!string.IsNullOrWhiteSpace(FwVersion))
            query = query.Where(d => d.FwVersion.Contains(FwVersion.Trim(), StringComparison.OrdinalIgnoreCase));

        return query.ToList();
    }

    private List<DeviceRecord> ApplyStatusFilter(IEnumerable<DeviceRecord> records)
    {
        if (string.IsNullOrWhiteSpace(Status))
            return records.ToList();

        var status = Status.Trim();
        return records
            .Where(d => d.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
