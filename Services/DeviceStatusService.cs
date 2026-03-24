using System.Net.NetworkInformation;
using Microsoft.Extensions.Caching.Memory;
using ZkbioDashboard.Models;

namespace ZkbioDashboard.Services;

public interface IDeviceStatusService
{
    Task<List<DeviceRecord>> ApplyStatusAsync(List<DeviceRecord> devices, CancellationToken cancellationToken = default);
}

public class DeviceStatusService : IDeviceStatusService
{
    private static readonly TimeSpan StatusCacheDuration = TimeSpan.FromSeconds(20);
    private const int MaxConcurrency = 20;
    private const int PingAttempts = 3;
    private const int PingTimeoutMs = 3000;
    private const int RetryDelayMs = 150;

    private readonly IMemoryCache _cache;

    public DeviceStatusService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<List<DeviceRecord>> ApplyStatusAsync(List<DeviceRecord> devices, CancellationToken cancellationToken = default)
    {
        if (devices.Count == 0)
            return devices;

        using var throttler = new SemaphoreSlim(MaxConcurrency);
        var tasks = devices.Select(async d =>
        {
            await throttler.WaitAsync(cancellationToken);
            try
            {
                var status = await GetPingStatusAsync(d.IpAddress, cancellationToken);
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

    private async Task<string> GetPingStatusAsync(string? ipAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return "Offline";

        var normalizedIp = ipAddress.Trim();
        var cacheKey = $"DeviceStatus::{normalizedIp}";
        if (_cache.TryGetValue<string>(cacheKey, out var cachedStatus) && !string.IsNullOrWhiteSpace(cachedStatus))
            return cachedStatus;

        var status = await ProbePingStatusAsync(normalizedIp, cancellationToken);
        _cache.Set(cacheKey, status, StatusCacheDuration);
        return status;
    }

    private static async Task<string> ProbePingStatusAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            for (var i = 0; i < PingAttempts; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var reply = await ping.SendPingAsync(ipAddress, PingTimeoutMs);
                    if (reply.Status == IPStatus.Success)
                        return "Online";
                }
                catch
                {
                    // Continue retrying. Some devices drop sporadic ICMP packets.
                }

                await Task.Delay(RetryDelayMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Any probe-level exception is treated as offline for this refresh.
        }

        return "Offline";
    }
}
