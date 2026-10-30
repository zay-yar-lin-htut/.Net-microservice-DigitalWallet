using MassTransit;
using Microsoft.EntityFrameworkCore;
using TransferService.Contracts;
using TransferService.Data;
using TransferService.Entities;

namespace TransferService.Services;

public sealed class TransferService(
    TransferDbContext db,
    IPublishEndpoint publishEndpoint,
    ILogger<TransferService> logger) : ITransferService
{
    public async Task<TransferResponse> CreateAsync(
        Guid senderUserId,
        CreateTransferRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (senderUserId == request.ReceiverUserId)
        {
            throw new SameUserTransferException();
        }

        var existing = await db.Transfers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.SenderUserId != senderUserId || existing.Amount != request.Amount ||
                existing.ReceiverUserId != request.ReceiverUserId)
            {
                throw new TransferOperationException("Idempotency key is already used for another transfer.");
            }
            return ToResponse(existing);
        }

        var transfer = new Transfer
        {
            SenderUserId = senderUserId,
            ReceiverUserId = request.ReceiverUserId,
            Amount = request.Amount,
            Currency = request.Currency.ToUpperInvariant(),
            IdempotencyKey = idempotencyKey
        };

        db.Transfers.Add(transfer);
        await publishEndpoint.Publish(new DigitalWallet.Contracts.StartTransferCommand(transfer.Id), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Transfer {TransferId} accepted with idempotency key {IdempotencyKey}", transfer.Id, idempotencyKey);
        return ToResponse(transfer);
    }

    public Task<TransferResponse?> GetAsync(Guid transferId, Guid userId, CancellationToken cancellationToken) =>
        db.Transfers.AsNoTracking()
            .Where(x => x.Id == transferId && (x.SenderUserId == userId || x.ReceiverUserId == userId))
            .Select(x => new TransferResponse(x.Id, x.SenderUserId, x.ReceiverUserId, x.Amount, x.Currency,
                x.Status, x.FailureReason, x.CreatedAtUtc, x.CompletedAtUtc, x.FailedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TransferResponse>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Transfers.AsNoTracking()
            .Where(x => x.SenderUserId == userId || x.ReceiverUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new TransferResponse(x.Id, x.SenderUserId, x.ReceiverUserId, x.Amount, x.Currency,
                x.Status, x.FailureReason, x.CreatedAtUtc, x.CompletedAtUtc, x.FailedAtUtc))
            .ToListAsync(cancellationToken);

    private static TransferResponse ToResponse(Transfer x) => new(x.Id, x.SenderUserId, x.ReceiverUserId,
        x.Amount, x.Currency, x.Status, x.FailureReason, x.CreatedAtUtc, x.CompletedAtUtc, x.FailedAtUtc);
}
