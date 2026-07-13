using rental_ps_smart_billing.Data;
using rental_ps_smart_billing.Models;

namespace rental_ps_smart_billing.Services;

public interface IBillingPackageService
{
    Task<IReadOnlyList<BillingPackageListItem>> GetPackagesAsync(CancellationToken cancellationToken = default);
    Task<BillingPackage?> GetPackageByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PackageResult> CreatePackageAsync(
        string name,
        int durationMinutes,
        decimal price,
        string billingMode,
        CancellationToken cancellationToken = default);
    Task<PackageResult> UpdatePackageAsync(
        int id,
        string name,
        int durationMinutes,
        decimal price,
        string billingMode,
        bool isActive,
        CancellationToken cancellationToken = default);
    Task<PackageResult> DeletePackageAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class BillingPackageService : IBillingPackageService
{
    private readonly IBillingPackageRepository _repository;

    public BillingPackageService(IBillingPackageRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<BillingPackageListItem>> GetPackagesAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<BillingPackage?> GetPackageByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public async Task<PackageResult> CreatePackageAsync(
        string name,
        int durationMinutes,
        decimal price,
        string billingMode,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(name, durationMinutes, price, billingMode);
        if (validation is not null)
            return validation;

        name = name.Trim();
        billingMode = NormalizeMode(billingMode);
        if (BillingModes.IsOpenEnded(billingMode))
            durationMinutes = 0;

        await _repository.CreateAsync(name, durationMinutes, price, billingMode, cancellationToken);

        AppLog.Info($"Package created: {name} ({billingMode}, {durationMinutes} menit, Rp {price:N0})");
        return PackageResult.Succeeded();
    }

    public async Task<PackageResult> UpdatePackageAsync(
        int id,
        string name,
        int durationMinutes,
        decimal price,
        string billingMode,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(name, durationMinutes, price, billingMode);
        if (validation is not null)
            return validation;

        name = name.Trim();
        billingMode = NormalizeMode(billingMode);
        if (BillingModes.IsOpenEnded(billingMode))
            durationMinutes = 0;

        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return PackageResult.Failed("Paket tidak ditemukan.");

        if (!isActive && existing.IsActive
            && await _repository.HasActiveSessionsAsync(id, cancellationToken))
        {
            return PackageResult.Failed("Paket masih dipakai sesi aktif. Akhiri sesi terlebih dahulu.");
        }

        await _repository.UpdateAsync(id, name, durationMinutes, price, billingMode, isActive, cancellationToken);

        AppLog.Info($"Package updated: {name} ({billingMode}, {durationMinutes} menit, Rp {price:N0})");
        return PackageResult.Succeeded();
    }

    public async Task<PackageResult> DeletePackageAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return PackageResult.Failed("Paket tidak ditemukan.");

        if (!existing.IsActive)
            return PackageResult.Failed("Paket sudah nonaktif.");

        if (await _repository.HasActiveSessionsAsync(id, cancellationToken))
            return PackageResult.Failed("Paket masih dipakai sesi aktif. Akhiri sesi terlebih dahulu.");

        await _repository.DeactivateAsync(id, cancellationToken);

        AppLog.Info($"Package deactivated: {existing.Name}");
        return PackageResult.Succeeded();
    }

    private static PackageResult? Validate(string name, int durationMinutes, decimal price, string billingMode)
    {
        name = name.Trim();
        billingMode = NormalizeMode(billingMode);

        if (string.IsNullOrWhiteSpace(name))
            return PackageResult.Failed("Nama paket wajib diisi.");

        if (name.Length > 50)
            return PackageResult.Failed("Nama paket maksimal 50 karakter.");

        if (billingMode != BillingModes.Fixed && billingMode != BillingModes.OpenEnded)
            return PackageResult.Failed("Tipe paket tidak valid.");

        if (billingMode == BillingModes.Fixed)
        {
            if (durationMinutes < 1)
                return PackageResult.Failed("Durasi minimal 1 menit.");

            if (durationMinutes > 1440)
                return PackageResult.Failed("Durasi maksimal 1440 menit (24 jam).");
        }

        if (price <= 0)
            return PackageResult.Failed(
                BillingModes.IsOpenEnded(billingMode)
                    ? "Tarif per menit harus lebih dari 0."
                    : "Harga harus lebih dari 0.");

        return null;
    }

    private static string NormalizeMode(string billingMode) =>
        string.IsNullOrWhiteSpace(billingMode)
            ? BillingModes.Fixed
            : billingMode.Trim();
}
