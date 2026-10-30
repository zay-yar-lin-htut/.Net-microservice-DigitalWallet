using TransferService.Contracts;

namespace TransferService.Services;

public interface ITransferService
{
    Task<TransferResponse> CreateAsync(
        Guid senderUserId,
        CreateTransferRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<TransferResponse?> GetAsync(Guid transferId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TransferResponse>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
