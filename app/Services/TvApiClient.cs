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

    Task<TvConnectionTestResult> SendKeyAsync(
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Set overlay warning for Tizen app (polled via GET /api/tv-notification).
    /// </summary>
    Task<TvConnectionTestResult> SetTvNotificationAsync(
        int tvId,
        bool showWarning,
        string message,
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
            // power-on waits for Wake-on-LAN readiness (up to ~30s)
            Timeout = TimeSpan.FromSeconds(60)
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

    public Task<TvConnectionTestResult> SendKeyAsync(
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        string key,
        CancellationToken cancellationToken = default) =>
        PostDeviceActionAsync(
            "tv/send-key",
            ipAddress,
            macAddress,
            wsPort,
            token,
            new Dictionary<string, object?> { ["key"] = key },
            cancellationToken);

    public async Task<TvConnectionTestResult> SetTvNotificationAsync(
        int tvId,
        bool showWarning,
        string message,
        CancellationToken cancellationToken = default)
    {
        var health = await EnsureHealthyAsync(cancellationToken);
        if (!health.Success)
            return health;

        var payload = new Dictionary<string, object?>
        {
            ["tv_id"] = tvId,
            ["show_warning"] = showWarning,
            ["message"] = message
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/tv-notification",
                payload,
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseResult(body);
        }
        catch (Exception ex)
        {
            AppLog.Error("TV API api/tv-notification failed", ex);
            return TvConnectionTestResult.Failed($"Gagal set notifikasi TV: {ex.Message}");
        }
    }

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
            ? ReadTokenValue(tokenProp)
            : null;

        return success
            ? TvConnectionTestResult.Succeeded(message, returnedToken)
            : new TvConnectionTestResult
            {
                Success = false,
                Message = message,
                TestedAt = DateTime.UtcNow,
                Token = string.IsNullOrWhiteSpace(returnedToken) ? null : returnedToken
            };
    }

    private static string? ReadTokenValue(JsonElement tokenProp)
    {
        return tokenProp.ValueKind switch
        {
            JsonValueKind.String => tokenProp.GetString(),
            JsonValueKind.Number => tokenProp.GetRawText(),
            _ => null
        };
    }

    public void Dispose() => _httpClient.Dispose();
}
