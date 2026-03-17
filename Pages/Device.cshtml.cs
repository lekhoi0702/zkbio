using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.NetworkInformation;
using ZkbioDashboard.Helpers;
using ZkbioDashboard.Models;
using ZkbioDashboard.Services;

namespace ZkbioDashboard.Pages;

public class DeviceModel : PageModel
{
    private readonly ITransactionService _transactionService;

    public DeviceModel(ITransactionService transactionService)
    {
        _transactionService = transactionService;
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
        var data = (await _transactionService.GetDevicesAsync(Factory)).ToList();
        var filtered = ApplyFilters(data);

        if (!string.IsNullOrWhiteSpace(Status))
        {
            var withStatus = await ApplyStatusAsync(filtered);
            var statusFiltered = ApplyStatusFilter(withStatus);
            TotalCount = statusFiltered.Count;
            Records = statusFiltered
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }
        else
        {
            TotalCount = filtered.Count;
            var paged = filtered
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();
            Records = await ApplyStatusAsync(paged);
        }
    }

    public async Task<JsonResult> OnGetDataAsync()
    {
        Factories = AttendanceOptions.Factories.ToList();
        var data = (await _transactionService.GetDevicesAsync(Factory)).ToList();
        var filtered = ApplyFilters(data);
        List<DeviceRecord> pageData;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            var withStatus = await ApplyStatusAsync(filtered);
            var statusFiltered = ApplyStatusFilter(withStatus);
            TotalCount = statusFiltered.Count;
            pageData = statusFiltered
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }
        else
        {
            TotalCount = filtered.Count;
            var paged = filtered
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();
            pageData = await ApplyStatusAsync(paged);
        }

        return new JsonResult(new
        {
            data = pageData.Select(d => new
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

    private static async Task<List<DeviceRecord>> ApplyStatusAsync(List<DeviceRecord> devices)
    {
        if (devices.Count == 0)
            return devices;

        var throttler = new SemaphoreSlim(20);
        var tasks = devices.Select(async d =>
        {
            await throttler.WaitAsync();
            try
            {
                var status = await GetPingStatusAsync(d.IpAddress);
                return new DeviceRecord
                {
                    DevAlias = d.DevAlias,
                    SN = d.SN,
                    AreaName = d.AreaName,
                    IpAddress = d.IpAddress,
                    Status = status,
                    DeviceName = d.DeviceName,
                    IsRegistrationDevice = d.IsRegistrationDevice,
                    FwVersion = d.FwVersion
                };
            }
            finally
            {
                throttler.Release();
            }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private static async Task<string> GetPingStatusAsync(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return "Offline";

        try
        {
            const int attempts = 3;
            const int timeoutMs = 3000;

            using var ping = new Ping();
            for (var i = 0; i < attempts; i++)
            {
                try
                {
                    var reply = await ping.SendPingAsync(ipAddress.Trim(), timeoutMs);
                    if (reply.Status == IPStatus.Success)
                        return "Online";
                }
                catch
                {
                    // swallow individual ping errors and retry
                }

                await Task.Delay(150);
            }

            return "Offline";
        }
        catch
        {
            return "Offline";
        }
    }
}
