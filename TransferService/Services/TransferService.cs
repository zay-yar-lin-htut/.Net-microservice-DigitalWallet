using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TransferService.Contracts;
using TransferService.Data;
using TransferService.Entities;
using TransferService.Events;
using TransferService.Messaging;

namespace TransferService.Services;

public sealed class TransferService(
    TransferDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IEventPublisher eventPublisher) : ITransferService
{
    public async Task<TransferResponse> CreateAsync(
        Guid senderUserId,
        CreateTransferRequest request,
        CancellationToken cancellationToken)
    {
        if (senderUserId == request.ReceiverUserId)
        {
            throw new SameUserTransferException();
        }

        var transfer = new Transfer
        {
            SenderUserId = senderUserId,
            ReceiverUserId = request.ReceiverUserId,
            Amount = request.Amount,
            Currency = request.Currency.ToUpperInvariant()
        };

        db.Transfers.Add(transfer);
        await db.SaveChangesAsync(cancellationToken);

        var walletDebited = false;
        var walletCredited = false;

        try
        {
            await WalletOperationAsync("debit", senderUserId, request.Amount, cancellationToken);
            walletDebited = true;

            await WalletOperationAsync("credit", request.ReceiverUserId, request.Amount, cancellationToken);
            walletCredited = true;

            await eventPublisher.PublishTransferCompletedAsync(
                new TransferCompletedEvent(
                    transfer.Id,
                    senderUserId,
                    request.ReceiverUserId,
                    request.Amount,
                    request.Currency.ToUpperInvariant(),
                    DateTime.UtcNow),
                cancellationToken);

            transfer.Status = "Completed";
            transfer.CompletedAtUtc = DateTime.UtcNow;
        }
        catch (Exception exception)
        {
            if (walletCredited)
            {
                await TryWalletOperationAsync(
                    "debit",
                    request.ReceiverUserId,
                    request.Amount,
                    cancellationToken);
            }

            if (walletDebited)
            {
                await TryWalletOperationAsync(
                    "credit",
                    senderUserId,
                    request.Amount,
                    cancellationToken);
            }

            transfer.Status = "Failed";
            transfer.FailureReason = exception.Message[..Math.Min(exception.Message.Length, 500)];
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(transfer);
    }

    public async Task<IReadOnlyList<TransferResponse>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await db.Transfers
            .AsNoTracking()
            .Where(transfer =>
                transfer.SenderUserId == userId ||
                transfer.ReceiverUserId == userId)
            .OrderByDescending(transfer => transfer.CreatedAtUtc)
            .Select(transfer => new TransferResponse(
                transfer.Id,
                transfer.SenderUserId,
                transfer.ReceiverUserId,
                transfer.Amount,
                transfer.Currency,
                transfer.Status,
                transfer.FailureReason,
                transfer.CreatedAtUtc,
                transfer.CompletedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private async Task WalletOperationAsync(
        string operation,
        Guid userId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("wallet");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/wallet/internal/{operation}");
        request.Headers.Add("X-Internal-Api-Key", GetInternalApiKey());
        request.Content = JsonContent.Create(new { userId, amount });

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new TransferOperationException($"Wallet operation failed: {message}");
        }
    }

    private async Task TryWalletOperationAsync(
        string operation,
        Guid userId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        try
        {
            await WalletOperationAsync(operation, userId, amount, cancellationToken);
        }
        catch
        {
            // The original failure is retained on the transfer record.
        }
    }

    private string GetInternalApiKey() =>
        configuration["Internal:ApiKey"]
        ?? throw new InvalidOperationException("Internal API key is missing.");

    private static TransferResponse ToResponse(Transfer transfer) => new(
        transfer.Id,
        transfer.SenderUserId,
        transfer.ReceiverUserId,
        transfer.Amount,
        transfer.Currency,
        transfer.Status,
        transfer.FailureReason,
        transfer.CreatedAtUtc,
        transfer.CompletedAtUtc);
}
