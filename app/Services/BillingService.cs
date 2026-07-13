using rental_ps_smart_billing.Data;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Services;

public interface IBillingService
{
    Task<IReadOnlyList<UnitCardItem>> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingPackage>> GetPackagesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingPackage>> GetFixedPackagesAsync(CancellationToken cancellationToken = default);
    Task<BillingResult> StartSessionAsync(
        int smartTvId,
        int packageId,
        string? customerName,
        int? startedByUserId,
        CancellationToken cancellationToken = default);
    Task<BillingResult> ExtendSessionAsync(
        int sessionId,
        int packageId,
        CancellationToken cancellationToken = default);
    Task<BillingResult> EndSessionAsync(
        int sessionId,
        CancellationToken cancellationToken = default);
    Task AutoEndExpiredAsync(CancellationToken cancellationToken = default);
}

public sealed class BillingService : IBillingService
{
    private readonly IRentalSessionRepository _sessions;
    private readonly IBillingPackageRepository _packages;
    private readonly ISmartTvRepository _smartTvs;
    private readonly ITvApiClient _tvApi;

    public BillingService(
        IRentalSessionRepository sessions,
        IBillingPackageRepository packages,
        ISmartTvRepository smartTvs,
        ITvApiClient tvApi)
    {
        _sessions = sessions;
        _packages = packages;
        _smartTvs = smartTvs;
        _tvApi = tvApi;
    }

    public Task<IReadOnlyList<UnitCardItem>> GetDashboardAsync(CancellationToken cancellationToken = default) =>
        _sessions.GetDashboardAsync(cancellationToken);

    public Task<IReadOnlyList<BillingPackage>> GetPackagesAsync(CancellationToken cancellationToken = default) =>
        _packages.GetActiveAsync(cancellationToken);

    public async Task<IReadOnlyList<BillingPackage>> GetFixedPackagesAsync(CancellationToken cancellationToken = default)
    {
        var packages = await _packages.GetActiveAsync(cancellationToken);
        return packages.Where(p => !p.IsOpenEnded).ToList();
    }

    public async Task<BillingResult> StartSessionAsync(
        int smartTvId,
        int packageId,
        string? customerName,
        int? startedByUserId,
        CancellationToken cancellationToken = default)
    {
        var tv = await _smartTvs.GetByIdAsync(smartTvId, cancellationToken);
        if (tv is null || !tv.IsActive)
            return BillingResult.Failed("Smart TV tidak ditemukan atau nonaktif.");

        var existing = await _sessions.GetActiveByTvIdAsync(smartTvId, cancellationToken);
        if (existing is not null)
            return BillingResult.Failed("TV ini masih memiliki sesi aktif.");

        var package = await _packages.GetByIdAsync(packageId, cancellationToken);
        if (package is null || !package.IsActive)
            return BillingResult.Failed("Paket tidak valid.");

        var name = string.IsNullOrWhiteSpace(customerName) ? "Guest" : customerName.Trim();
        var startedAt = DateTime.UtcNow;
        DateTime? endsAt = package.IsOpenEnded
            ? null
            : startedAt.AddMinutes(package.DurationMinutes);
        var amount = package.IsOpenEnded ? 0m : package.Price;

        await _sessions.CreateAsync(
            smartTvId,
            packageId,
            name,
            startedAt,
            endsAt,
            amount,
            startedByUserId,
            cancellationToken);

        AppLog.Info($"Session started: TV={tv.Name}, package={package.Name}, mode={package.BillingMode}, customer={name}");

        string? warning = null;
        var token = tv.Token;

        var powerOn = await _tvApi.PowerOnAsync(
            tv.IpAddress, tv.MacAddress, tv.WsPort, token, cancellationToken);
        token = await PersistTokenAsync(smartTvId, token, powerOn, cancellationToken);
        if (!powerOn.Success)
        {
            warning = $"Sesi dibuat, tetapi power-on gagal: {powerOn.Message}";
            return BillingResult.Succeeded(warning, amount);
        }

        var splash = await _tvApi.ShowSplashAsync(
            tv.IpAddress,
            tv.MacAddress,
            tv.WsPort,
            token,
            tv.Name,
            package.Name,
            name,
            cancellationToken);
        token = await PersistTokenAsync(smartTvId, token, splash, cancellationToken);

        if (!splash.Success)
            warning = $"Splash gagal: {splash.Message}";

        return BillingResult.Succeeded(warning, amount);
    }

    public async Task<BillingResult> ExtendSessionAsync(
        int sessionId,
        int packageId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || session.Status != "Active")
            return BillingResult.Failed("Sesi aktif tidak ditemukan.");

        if (session.IsOpenEnded || session.EndsAt is null)
            return BillingResult.Failed("Sesi Free Play tidak bisa ditambah waktu. Akhiri dengan BAYAR.");

        var package = await _packages.GetByIdAsync(packageId, cancellationToken);
        if (package is null || !package.IsActive)
            return BillingResult.Failed("Paket tidak valid.");

        if (package.IsOpenEnded)
            return BillingResult.Failed("Tidak bisa menambah waktu dengan paket Free Play.");

        var baseTime = session.EndsAt.Value > DateTime.UtcNow ? session.EndsAt.Value : DateTime.UtcNow;
        var newEndsAt = baseTime.AddMinutes(package.DurationMinutes);
        var newAmount = (session.Amount ?? 0) + package.Price;

        await _sessions.ExtendAsync(sessionId, newEndsAt, newAmount, cancellationToken);

        var tv = await _smartTvs.GetByIdAsync(session.SmartTvId, cancellationToken);
        string? warning = null;
        if (tv is not null)
        {
            var splash = await _tvApi.ShowSplashAsync(
                tv.IpAddress,
                tv.MacAddress,
                tv.WsPort,
                tv.Token,
                tv.Name,
                package.Name,
                session.CustomerName ?? "Guest",
                cancellationToken);
            await PersistTokenAsync(tv.Id, tv.Token, splash, cancellationToken);

            if (!splash.Success)
                warning = $"Waktu ditambah, tetapi splash gagal: {splash.Message}";
        }

        AppLog.Info($"Session extended: id={sessionId}, +{package.Name}");
        return BillingResult.Succeeded(warning, newAmount);
    }

    public async Task<BillingResult> EndSessionAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || session.Status != "Active")
            return BillingResult.Failed("Sesi aktif tidak ditemukan.");

        var endedAt = DateTime.UtcNow;
        decimal amount;
        if (session.IsOpenEnded)
        {
            var rate = session.PackagePrice ?? 0;
            amount = BillingCalculator.CalculateOpenEndedAmount(session.StartedAt, endedAt, rate);
            var minutes = BillingCalculator.BillableMinutes(session.StartedAt, endedAt);
            AppLog.Info($"Open-ended session billed: id={sessionId}, minutes={minutes}, amount={amount}");
        }
        else
        {
            amount = session.Amount ?? session.PackagePrice ?? 0;
        }

        // Persist completion first so dashboard/timer can stop before TV power-off.
        await _sessions.CompleteAsync(sessionId, endedAt, amount, cancellationToken);
        AppLog.Info($"Session ended: id={sessionId}, amount={amount}");

        string? warning = null;
        var tv = await _smartTvs.GetByIdAsync(session.SmartTvId, cancellationToken);
        if (tv is not null)
        {
            try
            {
                var powerOff = await _tvApi.PowerOffAsync(
                    tv.IpAddress, tv.MacAddress, tv.WsPort, tv.Token, cancellationToken);
                await PersistTokenAsync(tv.Id, tv.Token, powerOff, cancellationToken);
                if (!powerOff.Success)
                    warning = $"Sesi selesai, tetapi power-off gagal: {powerOff.Message}";
            }
            catch (Exception ex)
            {
                AppLog.Error($"Power-off failed after session end id={sessionId}", ex);
                warning = "Sesi selesai, tetapi power-off gagal.";
            }
        }

        return BillingResult.Succeeded(warning, amount);
    }

    private async Task<string?> PersistTokenAsync(
        int smartTvId,
        string? currentToken,
        TvConnectionTestResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(result.Token)
            || string.Equals(result.Token, currentToken, StringComparison.Ordinal))
            return currentToken;

        await _smartTvs.UpdateTokenAsync(smartTvId, result.Token, cancellationToken);
        AppLog.Info($"Smart TV token updated: id={smartTvId}");
        return result.Token;
    }

    public async Task AutoEndExpiredAsync(CancellationToken cancellationToken = default)
    {
        var expired = await _sessions.GetExpiredActiveAsync(cancellationToken);
        foreach (var session in expired)
        {
            try
            {
                await EndSessionAsync(session.Id, cancellationToken);
                AppLog.Info($"Auto-ended expired session {session.Id}");
            }
            catch (Exception ex)
            {
                AppLog.Error($"Failed to auto-end session {session.Id}", ex);
            }
        }
    }
}
