using rental_ps_smart_billing.Data;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Services;

public interface ITvModelService
{
    Task<IReadOnlyList<TvModelListItem>> GetModelsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TvModelListItem>> GetActiveModelsAsync(CancellationToken cancellationToken = default);
    Task<TvModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<SleepTimerProfile> ResolveSleepProfileForSmartTvAsync(
        int smartTvId,
        CancellationToken cancellationToken = default);
    Task<TvModelResult> CreateAsync(
        string code,
        string name,
        string brand,
        string sleepTimerMode,
        int sleepTimerMinutes,
        double sleepTimerKeyDelaySeconds,
        string sleepTimerConfirmKeys,
        CancellationToken cancellationToken = default);
    Task<TvModelResult> UpdateAsync(
        int id,
        string code,
        string name,
        string brand,
        string sleepTimerMode,
        int sleepTimerMinutes,
        double sleepTimerKeyDelaySeconds,
        string sleepTimerConfirmKeys,
        bool isActive,
        CancellationToken cancellationToken = default);
    Task<TvModelResult> DeactivateAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class TvModelService : ITvModelService
{
    private readonly ITvModelRepository _repository;

    public TvModelService(ITvModelRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<TvModelListItem>> GetModelsAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<IReadOnlyList<TvModelListItem>> GetActiveModelsAsync(CancellationToken cancellationToken = default) =>
        _repository.GetActiveAsync(cancellationToken);

    public Task<TvModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public async Task<SleepTimerProfile> ResolveSleepProfileForSmartTvAsync(
        int smartTvId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetSleepProfileBySmartTvIdAsync(smartTvId, cancellationToken);
        if (profile is not null)
            return profile;

        return SleepTimerProfile.FromDefaults(BillingCalculator.SleepTimerMinutes);
    }

    public async Task<TvModelResult> CreateAsync(
        string code,
        string name,
        string brand,
        string sleepTimerMode,
        int sleepTimerMinutes,
        double sleepTimerKeyDelaySeconds,
        string sleepTimerConfirmKeys,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(
            code, name, brand, sleepTimerMode, sleepTimerMinutes,
            sleepTimerKeyDelaySeconds, sleepTimerConfirmKeys, null, cancellationToken);
        if (!validation.Success)
            return validation;

        await _repository.CreateAsync(
            code.Trim().ToUpperInvariant(),
            name.Trim(),
            brand.Trim(),
            NormalizeMode(sleepTimerMode),
            sleepTimerMinutes,
            sleepTimerKeyDelaySeconds,
            NormalizeKeys(sleepTimerConfirmKeys),
            cancellationToken);

        AppLog.Info($"TV model created: {code.Trim().ToUpperInvariant()}");
        return TvModelResult.Succeeded();
    }

    public async Task<TvModelResult> UpdateAsync(
        int id,
        string code,
        string name,
        string brand,
        string sleepTimerMode,
        int sleepTimerMinutes,
        double sleepTimerKeyDelaySeconds,
        string sleepTimerConfirmKeys,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return TvModelResult.Failed("Model TV tidak ditemukan.");

        var validation = await ValidateAsync(
            code, name, brand, sleepTimerMode, sleepTimerMinutes,
            sleepTimerKeyDelaySeconds, sleepTimerConfirmKeys, id, cancellationToken);
        if (!validation.Success)
            return validation;

        await _repository.UpdateAsync(
            id,
            code.Trim().ToUpperInvariant(),
            name.Trim(),
            brand.Trim(),
            NormalizeMode(sleepTimerMode),
            sleepTimerMinutes,
            sleepTimerKeyDelaySeconds,
            NormalizeKeys(sleepTimerConfirmKeys),
            isActive,
            cancellationToken);

        AppLog.Info($"TV model updated: {code.Trim().ToUpperInvariant()}");
        return TvModelResult.Succeeded();
    }

    public async Task<TvModelResult> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return TvModelResult.Failed("Model TV tidak ditemukan.");

        if (!existing.IsActive)
            return TvModelResult.Failed("Model TV sudah nonaktif.");

        if (await _repository.IsUsedBySmartTvAsync(id, cancellationToken))
            return TvModelResult.Failed("Model masih dipakai Smart TV aktif. Ubah model TV dulu.");

        await _repository.DeactivateAsync(id, cancellationToken);
        AppLog.Info($"TV model deactivated: {existing.Code}");
        return TvModelResult.Succeeded();
    }

    private async Task<TvModelResult> ValidateAsync(
        string code,
        string name,
        string brand,
        string sleepTimerMode,
        int sleepTimerMinutes,
        double sleepTimerKeyDelaySeconds,
        string sleepTimerConfirmKeys,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        code = code.Trim();
        name = name.Trim();
        brand = brand.Trim();

        if (string.IsNullOrWhiteSpace(code))
            return TvModelResult.Failed("Kode model wajib diisi.");

        if (code.Length > 50)
            return TvModelResult.Failed("Kode model maksimal 50 karakter.");

        if (string.IsNullOrWhiteSpace(name))
            return TvModelResult.Failed("Nama model wajib diisi.");

        if (string.IsNullOrWhiteSpace(brand))
            return TvModelResult.Failed("Merek wajib diisi.");

        var mode = NormalizeMode(sleepTimerMode);
        if (mode is not ("menu" or "cycle"))
            return TvModelResult.Failed("Mode Sleep Timer harus menu atau cycle.");

        if (sleepTimerMinutes is < 1 or > 180)
            return TvModelResult.Failed("Durasi Sleep Timer harus 1–180 menit.");

        if (sleepTimerKeyDelaySeconds is < 0.2 or > 5)
            return TvModelResult.Failed("Delay antar tombol harus 0.2–5 detik.");

        var keys = NormalizeKeys(sleepTimerConfirmKeys);
        if (mode == "menu" && SleepTimerProfile.ParseConfirmKeys(keys).Count == 0)
            return TvModelResult.Failed("Confirm keys wajib diisi untuk mode menu.");

        if (await _repository.CodeExistsAsync(code.ToUpperInvariant(), excludeId, cancellationToken))
            return TvModelResult.Failed("Kode model sudah dipakai model aktif lain.");

        return TvModelResult.Succeeded();
    }

    private static string NormalizeMode(string mode) =>
        string.IsNullOrWhiteSpace(mode) ? "menu" : mode.Trim().ToLowerInvariant();

    private static string NormalizeKeys(string keys) =>
        string.Join(",", SleepTimerProfile.ParseConfirmKeys(keys));
}
