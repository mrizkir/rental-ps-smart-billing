using rental_ps_smart_billing.Data;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Services;

public interface IBillingService
{
    Task<IReadOnlyList<UnitCardItem>> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingPackage>> GetPackagesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingPackage>> GetFixedPackagesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingPackage>> GetOpenEndedPackagesAsync(CancellationToken cancellationToken = default);
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
    Task<BillingResult> ExtendSessionByCustomAsync(
        int sessionId,
        int minutes,
        decimal price,
        CancellationToken cancellationToken = default);
    Task<BillingResult> ConvertToFreePlayAsync(
        int sessionId,
        int freePlayPackageId,
        CancellationToken cancellationToken = default);
    Task<BillingResult> EndSessionAsync(
        int sessionId,
        CancellationToken cancellationToken = default);
    Task<BillingResult> ShowSessionEndWarningAsync(
        int smartTvId,
        string? overlayMessage = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutoEndedSessionItem>> AutoEndExpiredAsync(CancellationToken cancellationToken = default);
    Task<RevenueReportResult> GetRevenueReportAsync(
        DateTime fromLocalDate,
        DateTime toLocalDate,
        CancellationToken cancellationToken = default);
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

    public async Task<IReadOnlyList<BillingPackage>> GetOpenEndedPackagesAsync(CancellationToken cancellationToken = default)
    {
        var packages = await _packages.GetActiveAsync(cancellationToken);
        return packages.Where(p => p.IsOpenEnded).ToList();
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

        await PushSessionOverlayAsync(
            smartTvId,
            package.Name,
            name,
            package.BillingMode,
            endsAt,
            cancellationToken);

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
        var package = await _packages.GetByIdAsync(packageId, cancellationToken);
        if (package is null || !package.IsActive)
            return BillingResult.Failed("Paket tidak valid.");

        if (package.IsOpenEnded)
            return BillingResult.Failed("Tidak bisa menambah waktu dengan paket Free Play.");

        return await ExtendSessionCoreAsync(
            sessionId,
            package.DurationMinutes,
            package.Price,
            package.Name,
            cancellationToken);
    }

    public Task<BillingResult> ExtendSessionByCustomAsync(
        int sessionId,
        int minutes,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        if (minutes < 1)
            return Task.FromResult(BillingResult.Failed("Durasi minimal 1 menit."));

        if (minutes > 1440)
            return Task.FromResult(BillingResult.Failed("Durasi maksimal 1440 menit (24 jam)."));

        if (price < 0)
            return Task.FromResult(BillingResult.Failed("Harga tidak boleh negatif."));

        return ExtendSessionCoreAsync(
            sessionId,
            minutes,
            price,
            $"+{minutes} menit",
            cancellationToken);
    }

    private async Task<BillingResult> ExtendSessionCoreAsync(
        int sessionId,
        int minutes,
        decimal price,
        string label,
        CancellationToken cancellationToken)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || session.Status != "Active")
            return BillingResult.Failed("Sesi aktif tidak ditemukan.");

        if (session.IsOpenEnded || session.EndsAt is null)
            return BillingResult.Failed("Sesi Free Play tidak bisa ditambah waktu. Akhiri dengan BAYAR.");

        var baseTime = session.EndsAt.Value > DateTime.UtcNow ? session.EndsAt.Value : DateTime.UtcNow;
        var newEndsAt = baseTime.AddMinutes(minutes);
        var newAmount = (session.Amount ?? 0) + price;

        await _sessions.ExtendAsync(sessionId, newEndsAt, newAmount, cancellationToken);

        var packageLabel = string.IsNullOrWhiteSpace(session.PackageName)
            ? label
            : session.PackageName;
        await PushSessionOverlayAsync(
            session.SmartTvId,
            packageLabel,
            session.CustomerName,
            BillingModes.Fixed,
            newEndsAt,
            cancellationToken);

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
                label,
                session.CustomerName ?? "Guest",
                cancellationToken);
            await PersistTokenAsync(tv.Id, tv.Token, splash, cancellationToken);

            if (!splash.Success)
                warning = $"Waktu ditambah, tetapi splash gagal: {splash.Message}";
        }

        AppLog.Info($"Session extended: id={sessionId}, {label}, +Rp {price:N0}");
        return BillingResult.Succeeded(warning, newAmount);
    }

    public async Task<BillingResult> ConvertToFreePlayAsync(
        int sessionId,
        int freePlayPackageId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
        if (session is null || session.Status != "Active")
            return BillingResult.Failed("Sesi aktif tidak ditemukan.");

        if (session.IsOpenEnded || session.EndsAt is null)
            return BillingResult.Failed("Sesi sudah Free Play.");

        var package = await _packages.GetByIdAsync(freePlayPackageId, cancellationToken);
        if (package is null || !package.IsActive || !package.IsOpenEnded)
            return BillingResult.Failed("Paket Free Play tidak valid.");

        var openEndedFrom = DateTime.UtcNow;
        await _sessions.ConvertToFreePlayAsync(sessionId, package.Id, openEndedFrom, cancellationToken);

        var prepaid = session.Amount ?? 0;
        await PushSessionOverlayAsync(
            session.SmartTvId,
            package.Name,
            session.CustomerName,
            BillingModes.OpenEnded,
            endsAtUtc: null,
            cancellationToken);

        string? warning = null;
        var tv = await _smartTvs.GetByIdAsync(session.SmartTvId, cancellationToken);
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
                warning = $"Diubah ke Free Play, tetapi splash gagal: {splash.Message}";
        }

        AppLog.Info(
            $"Session converted to Free Play: id={sessionId}, package={package.Name}, prepaid={prepaid}, openEndedFrom={openEndedFrom:O}");
        return BillingResult.Succeeded(warning, prepaid);
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
            var prepaid = session.Amount ?? 0;
            var rate = session.PackagePrice ?? 0;
            var from = session.OpenEndedBillingFrom;
            var openEndedAmount = BillingCalculator.CalculateOpenEndedAmount(from, endedAt, rate);
            amount = prepaid + openEndedAmount;
            var minutes = BillingCalculator.BillableOpenEndedMinutes(from, endedAt);
            AppLog.Info(
                $"Open-ended session billed: id={sessionId}, prepaid={prepaid}, minutes={minutes} (grace={BillingCalculator.FreePlayGraceMinutes}), openEnded={openEndedAmount}, total={amount}");
        }
        else
        {
            amount = session.Amount ?? session.PackagePrice ?? 0;
        }

        // Persist completion first so dashboard/timer can stop before TV power-off.
        await _sessions.CompleteAsync(sessionId, endedAt, amount, cancellationToken);
        AppLog.Info($"Session ended: id={sessionId}, amount={amount}");

        await ClearSessionOverlayAsync(session.SmartTvId, cancellationToken);

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

    public async Task<BillingResult> ShowSessionEndWarningAsync(
        int smartTvId,
        string? overlayMessage = null,
        CancellationToken cancellationToken = default)
    {
        var tv = await _smartTvs.GetByIdAsync(smartTvId, cancellationToken);
        if (tv is null || !tv.IsActive)
            return BillingResult.Failed("Smart TV tidak ditemukan atau nonaktif.");

        var message = string.IsNullOrWhiteSpace(overlayMessage)
            ? $"{BillingCalculator.SessionWarnMinutesBeforeEnd} menit lagi"
            : overlayMessage.Trim();

        try
        {
            var notify = await _tvApi.SetTvNotificationAsync(
                smartTvId,
                showWarning: true,
                message,
                cancellationToken);
            if (!notify.Success)
                return BillingResult.Failed(notify.Message);

            AppLog.Info($"Session end warning sent: TV={tv.Name}, message={message}");
            return BillingResult.Succeeded($"Peringatan dikirim ke overlay {tv.Name}.");
        }
        catch (Exception ex)
        {
            AppLog.Error($"Session end warning failed for TV id={smartTvId}", ex);
            return BillingResult.Failed("Gagal mengirim peringatan ke TV.");
        }
    }

    private async Task PushSessionOverlayAsync(
        int smartTvId,
        string packageName,
        string? customerName,
        string billingMode,
        DateTime? endsAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _tvApi.SetTvSessionOverlayAsync(
                smartTvId,
                active: true,
                packageName,
                customerName,
                billingMode,
                endsAtUtc,
                cancellationToken);
            if (!result.Success)
                AppLog.Warn($"Session overlay push failed for TV id={smartTvId}: {result.Message}");
        }
        catch (Exception ex)
        {
            AppLog.Error($"Session overlay push failed for TV id={smartTvId}", ex);
        }
    }

    private async Task ClearSessionOverlayAsync(int smartTvId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _tvApi.SetTvSessionOverlayAsync(
                smartTvId,
                active: false,
                cancellationToken: cancellationToken);
            if (!result.Success)
                AppLog.Warn($"Session overlay clear failed for TV id={smartTvId}: {result.Message}");
        }
        catch (Exception ex)
        {
            AppLog.Error($"Session overlay clear failed for TV id={smartTvId}", ex);
        }
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

    public async Task<IReadOnlyList<AutoEndedSessionItem>> AutoEndExpiredAsync(
        CancellationToken cancellationToken = default)
    {
        var expired = await _sessions.GetExpiredActiveAsync(cancellationToken);
        if (expired.Count == 0)
            return [];

        var ended = new List<AutoEndedSessionItem>(expired.Count);
        foreach (var session in expired)
        {
            try
            {
                var tv = await _smartTvs.GetByIdAsync(session.SmartTvId, cancellationToken);
                var tvName = tv?.Name ?? $"TV #{session.SmartTvId}";
                var result = await EndSessionAsync(session.Id, cancellationToken);
                if (!result.Success)
                {
                    AppLog.Error(
                        $"Failed to auto-end session {session.Id}: {result.ErrorMessage}");
                    continue;
                }

                ended.Add(new AutoEndedSessionItem
                {
                    SessionId = session.Id,
                    TvName = tvName,
                    CustomerName = session.CustomerName,
                    Amount = result.Amount ?? session.Amount ?? 0,
                    WarningMessage = result.WarningMessage
                });
                AppLog.Info($"Auto-ended expired session {session.Id}, amount={result.Amount}");
            }
            catch (Exception ex)
            {
                AppLog.Error($"Failed to auto-end session {session.Id}", ex);
            }
        }

        return ended;
    }

    public async Task<RevenueReportResult> GetRevenueReportAsync(
        DateTime fromLocalDate,
        DateTime toLocalDate,
        CancellationToken cancellationToken = default)
    {
        if (toLocalDate.Date < fromLocalDate.Date)
            (fromLocalDate, toLocalDate) = (toLocalDate, fromLocalDate);

        var fromUtc = DateTime.SpecifyKind(fromLocalDate.Date, DateTimeKind.Local).ToUniversalTime();
        var toUtcExclusive = DateTime.SpecifyKind(toLocalDate.Date.AddDays(1), DateTimeKind.Local)
            .ToUniversalTime();

        var items = await _sessions.GetCompletedRevenueAsync(fromUtc, toUtcExclusive, cancellationToken);
        return new RevenueReportResult
        {
            FromLocalDate = fromLocalDate.Date,
            ToLocalDate = toLocalDate.Date,
            SessionCount = items.Count,
            TotalAmount = items.Sum(i => i.Amount),
            Items = items
        };
    }
}
