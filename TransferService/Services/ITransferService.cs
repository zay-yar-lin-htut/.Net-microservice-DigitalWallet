using TransferService.Contracts;

namespace TransferService.Services;

public interface ITransferService
{
    Task<TransferResponse> CreateAsync(
        Guid senderUserId,
        CreateTransferRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TransferResponse>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
