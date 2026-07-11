using System.Net.Http.Json;
using System.Text.Json;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Services;

public interface ITvApiClient
{
    Task<TvConnectionTestResult> TestConnectionAsync(
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        CancellationToken cancellationToken = default);

    Task<TvConnectionTestResult> PowerOnAsync(
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        CancellationToken cancellationToken = default);

    Task<TvConnectionTestResult> PowerOffAsync(
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        CancellationToken cancellationToken = default);

    Task<TvConnectionTestResult> ShowSplashAsync(
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        string unitName,
        string durationLabel,
        string customerName,
        CancellationToken cancellationToken = default);
}

public sealed class TvApiClient : ITvApiClient, IDisposable
{
    private readonly HttpClient _httpClient;

    public TvApiClient(string baseUrl)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<TvConnectionTestResult> TestConnectionAsync(
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        CancellationToken cancellationToken = default)
    {
        var health = await EnsureHealthyAsync(cancellationToken);
        if (!health.Success)
            return health;

        var query =
            $"tv/status?tv_ip={Uri.EscapeDataString(ipAddress)}" +
            $"&tv_mac={Uri.EscapeDataString(macAddress)}" +
            $"&ws_port={wsPort}";

        if (!string.IsNullOrWhiteSpace(token))
            query += $"&tv_token={Uri.EscapeDataString(token)}";

        return await GetJsonResultAsync(query, cancellationToken);
    }

    public Task<TvConnectionTestResult> PowerOnAsync(
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        CancellationToken cancellationToken = default) =>
        PostDeviceActionAsync("tv/power-on", ipAddress, macAddress, wsPort, token, null, cancellationToken);

    public Task<TvConnectionTestResult> PowerOffAsync(
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        CancellationToken cancellationToken = default) =>
        PostDeviceActionAsync("tv/power-off", ipAddress, macAddress, wsPort, token, null, cancellationToken);

    public Task<TvConnectionTestResult> ShowSplashAsync(
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        string unitName,
        string durationLabel,
        string customerName,
        CancellationToken cancellationToken = default) =>
        PostDeviceActionAsync(
            "splash/show",
            ipAddress,
            macAddress,
            wsPort,
            token,
            new Dictionary<string, object?>
            {
                ["unit"] = unitName,
                ["durasi"] = durationLabel,
                ["nama"] = customerName
            },
            cancellationToken);

    private async Task<TvConnectionTestResult> PostDeviceActionAsync(
        string path,
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        Dictionary<string, object?>? extra,
        CancellationToken cancellationToken)
    {
        var health = await EnsureHealthyAsync(cancellationToken);
        if (!health.Success)
            return health;

        var payload = new Dictionary<string, object?>
        {
            ["tv_ip"] = ipAddress,
            ["tv_mac"] = macAddress,
            ["ws_port"] = wsPort
        };

        if (!string.IsNullOrWhiteSpace(token))
            payload["tv_token"] = token;

        if (extra is not null)
        {
            foreach (var pair in extra)
                payload[pair.Key] = pair.Value;
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(path, payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseResult(body);
        }
        catch (Exception ex)
        {
            AppLog.Error($"TV API {path} failed", ex);
            return TvConnectionTestResult.Failed($"Gagal memanggil TV service: {ex.Message}");
        }
    }

    private async Task<TvConnectionTestResult> EnsureHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var healthResponse = await _httpClient.GetAsync("health", cancellationToken);
            if (!healthResponse.IsSuccessStatusCode)
                return TvConnectionTestResult.Failed("Python TV service tidak merespons.");
            return TvConnectionTestResult.Succeeded("ok");
        }
        catch (Exception ex)
        {
            AppLog.Warn($"TV service health check failed: {ex.Message}");
            return TvConnectionTestResult.Failed(
                "Python TV service tidak berjalan. Jalankan: cd python && python tv_service.py");
        }
    }

    private async Task<TvConnectionTestResult> GetJsonResultAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseResult(body);
        }
        catch (Exception ex)
        {
            AppLog.Error("TV status check failed", ex);
            return TvConnectionTestResult.Failed($"Gagal menghubungi TV: {ex.Message}");
        }
    }

    private static TvConnectionTestResult ParseResult(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();
        var message = root.TryGetProperty("message", out var msgProp)
            ? msgProp.GetString() ?? string.Empty
            : string.Empty;
        var returnedToken = root.TryGetProperty("token", out var tokenProp)
            ? tokenProp.GetString()
            : null;

        return success
            ? TvConnectionTestResult.Succeeded(message, returnedToken)
            : TvConnectionTestResult.Failed(message);
    }

    public void Dispose() => _httpClient.Dispose();
}
