using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using rental_ps_smart_billing.Data;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Services;

public interface ISmartTvService
{
    IReadOnlyList<string> GetBrands();
    Task<IReadOnlyList<SmartTvListItem>> GetSmartTvsAsync(CancellationToken cancellationToken = default);
    Task<SmartTvEditDetails?> GetForEditAsync(int id, CancellationToken cancellationToken = default);
    Task<SmartTvResult> CreateAsync(
        string name,
        string brand,
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        CancellationToken cancellationToken = default);
    Task<SmartTvResult> UpdateAsync(
        int id,
        string name,
        string brand,
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        bool isActive,
        CancellationToken cancellationToken = default);
    Task<SmartTvResult> DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task<TvConnectionTestResult> TestConnectionAsync(
        SmartTvTestRequest request,
        CancellationToken cancellationToken = default);
    Task<TvConnectionTestResult> TestConnectionAsync(
        int id,
        CancellationToken cancellationToken = default);
}

public sealed class SmartTvService : ISmartTvService
{
    private static readonly Regex MacRegex = new(
        @"^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$",
        RegexOptions.Compiled);

    private readonly ISmartTvRepository _repository;
    private readonly ITvApiClient _tvApiClient;

    public SmartTvService(ISmartTvRepository repository, ITvApiClient tvApiClient)
    {
        _repository = repository;
        _tvApiClient = tvApiClient;
    }

    public IReadOnlyList<string> GetBrands() => ["Samsung"];

    public Task<IReadOnlyList<SmartTvListItem>> GetSmartTvsAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<SmartTvEditDetails?> GetForEditAsync(int id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public async Task<SmartTvResult> CreateAsync(
        string name,
        string brand,
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(name, brand, ipAddress, macAddress, wsPort, token, null, cancellationToken);
        if (!validation.Success)
            return validation;

        await _repository.CreateAsync(
            name.Trim(),
            brand.Trim(),
            ipAddress.Trim(),
            NormalizeMac(macAddress),
            wsPort,
            NormalizeToken(token),
            cancellationToken);

        AppLog.Info($"Smart TV created: {name.Trim()} ({ipAddress.Trim()})");
        return SmartTvResult.Succeeded();
    }

    public async Task<SmartTvResult> UpdateAsync(
        int id,
        string name,
        string brand,
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return SmartTvResult.Failed("Smart TV tidak ditemukan.");

        var validation = await ValidateAsync(
            name, brand, ipAddress, macAddress, wsPort, token, id, cancellationToken);
        if (!validation.Success)
            return validation;

        await _repository.UpdateAsync(
            id,
            name.Trim(),
            brand.Trim(),
            ipAddress.Trim(),
            NormalizeMac(macAddress),
            wsPort,
            NormalizeToken(token),
            isActive,
            cancellationToken);

        AppLog.Info($"Smart TV updated: {name.Trim()} ({ipAddress.Trim()})");
        return SmartTvResult.Succeeded();
    }

    public async Task<SmartTvResult> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return SmartTvResult.Failed("Smart TV tidak ditemukan.");

        if (!existing.IsActive)
            return SmartTvResult.Failed("Smart TV sudah nonaktif.");

        await _repository.DeactivateAsync(id, cancellationToken);

        AppLog.Info($"Smart TV deactivated: {existing.Name}");
        return SmartTvResult.Succeeded();
    }

    public Task<TvConnectionTestResult> TestConnectionAsync(
        SmartTvTestRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateConnectionFields(
            request.IpAddress,
            request.MacAddress,
            request.WsPort,
            request.Token);

        if (!validation.Success)
        {
            return Task.FromResult(TvConnectionTestResult.Failed(validation.ErrorMessage ?? "Data tidak valid."));
        }

        return _tvApiClient.TestConnectionAsync(
            request.IpAddress.Trim(),
            NormalizeMac(request.MacAddress),
            request.WsPort,
            NormalizeToken(request.Token),
            cancellationToken);
    }

    public async Task<TvConnectionTestResult> TestConnectionAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var tv = await _repository.GetByIdAsync(id, cancellationToken);
        if (tv is null)
            return TvConnectionTestResult.Failed("Smart TV tidak ditemukan.");

        var result = await TestConnectionAsync(
            new SmartTvTestRequest
            {
                IpAddress = tv.IpAddress,
                MacAddress = tv.MacAddress,
                WsPort = tv.WsPort,
                Token = tv.Token
            },
            cancellationToken);

        var status = result.Success ? "success" : "failed";
        await _repository.UpdateTestResultAsync(id, status, result.Message, cancellationToken);

        if (!string.IsNullOrWhiteSpace(result.Token)
            && !string.Equals(result.Token, tv.Token, StringComparison.Ordinal))
        {
            await _repository.UpdateTokenAsync(id, result.Token, cancellationToken);
            AppLog.Info($"Smart TV token updated from pairing: {tv.Name}");
        }

        return result;
    }

    private async Task<SmartTvResult> ValidateAsync(
        string name,
        string brand,
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        name = name.Trim();
        brand = brand.Trim();
        ipAddress = ipAddress.Trim();
        macAddress = NormalizeMac(macAddress);

        if (string.IsNullOrWhiteSpace(name))
            return SmartTvResult.Failed("Nama TV wajib diisi.");

        if (string.IsNullOrWhiteSpace(brand))
            return SmartTvResult.Failed("Merek wajib dipilih.");

        if (!GetBrands().Contains(brand, StringComparer.OrdinalIgnoreCase))
            return SmartTvResult.Failed("Merek tidak valid.");

        if (!IsValidIpv4(ipAddress))
            return SmartTvResult.Failed("IP Address tidak valid.");

        if (!MacRegex.IsMatch(macAddress))
            return SmartTvResult.Failed("MAC Address tidak valid. Format: XX:XX:XX:XX:XX:XX");

        if (wsPort is < 1 or > 65535)
            return SmartTvResult.Failed("Port WebSocket harus antara 1 dan 65535.");

        if (!string.IsNullOrWhiteSpace(token) && token.Trim().Length > 200)
            return SmartTvResult.Failed("Token terlalu panjang.");

        if (await _repository.IpExistsAsync(ipAddress, excludeId, cancellationToken))
            return SmartTvResult.Failed("IP Address sudah digunakan oleh TV aktif lain.");

        if (await _repository.MacExistsAsync(macAddress, excludeId, cancellationToken))
            return SmartTvResult.Failed("MAC Address sudah digunakan oleh TV aktif lain.");

        return SmartTvResult.Succeeded();
    }

    private static SmartTvResult ValidateConnectionFields(
        string ipAddress,
        string macAddress,
        int wsPort,
        string? token)
    {
        ipAddress = ipAddress.Trim();
        macAddress = NormalizeMac(macAddress);

        if (!IsValidIpv4(ipAddress))
            return SmartTvResult.Failed("IP Address tidak valid.");

        if (!MacRegex.IsMatch(macAddress))
            return SmartTvResult.Failed("MAC Address tidak valid. Format: XX:XX:XX:XX:XX:XX");

        if (wsPort is < 1 or > 65535)
            return SmartTvResult.Failed("Port WebSocket harus antara 1 dan 65535.");

        if (!string.IsNullOrWhiteSpace(token) && token.Trim().Length > 200)
            return SmartTvResult.Failed("Token terlalu panjang.");

        return SmartTvResult.Succeeded();
    }

    private static string NormalizeMac(string mac) => mac.Trim().ToUpperInvariant();

    private static string? NormalizeToken(string? token) =>
        string.IsNullOrWhiteSpace(token) ? null : token.Trim();

    private static bool IsValidIpv4(string ip) =>
        IPAddress.TryParse(ip, out var address) && address.AddressFamily == AddressFamily.InterNetwork;
}
