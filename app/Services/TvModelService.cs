using rental_ps_smart_billing.Data;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Services;

public interface ITvModelService
{
    Task<IReadOnlyList<TvModelListItem>> GetModelsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TvModelListItem>> GetActiveModelsAsync(CancellationToken cancellationToken = default);
    Task<TvModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TvModelResult> CreateAsync(
        string code,
        string name,
        string brand,
        CancellationToken cancellationToken = default);
    Task<TvModelResult> UpdateAsync(
        int id,
        string code,
        string name,
        string brand,
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

    public async Task<TvModelResult> CreateAsync(
        string code,
        string name,
        string brand,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(code, name, brand, null, cancellationToken);
        if (!validation.Success)
            return validation;

        await _repository.CreateAsync(
            code.Trim().ToUpperInvariant(),
            name.Trim(),
            brand.Trim(),
            cancellationToken);

        AppLog.Info($"TV model created: {code.Trim().ToUpperInvariant()}");
        return TvModelResult.Succeeded();
    }

    public async Task<TvModelResult> UpdateAsync(
        int id,
        string code,
        string name,
        string brand,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return TvModelResult.Failed("Model TV tidak ditemukan.");

        var validation = await ValidateAsync(code, name, brand, id, cancellationToken);
        if (!validation.Success)
            return validation;

        await _repository.UpdateAsync(
            id,
            code.Trim().ToUpperInvariant(),
            name.Trim(),
            brand.Trim(),
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

        if (await _repository.CodeExistsAsync(code.ToUpperInvariant(), excludeId, cancellationToken))
            return TvModelResult.Failed("Kode model sudah dipakai model aktif lain.");

        return TvModelResult.Succeeded();
    }
}
