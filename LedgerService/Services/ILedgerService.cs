using LedgerService.Contracts;

namespace LedgerService.Services;

public interface ILedgerService
{
    Task<LedgerEntryResponse> CreateAsync(
        Guid userId,
        CreateLedgerEntryRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LedgerEntryResponse>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
