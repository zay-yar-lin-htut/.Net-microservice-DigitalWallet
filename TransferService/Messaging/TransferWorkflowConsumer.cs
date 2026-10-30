using DigitalWallet.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using TransferService.Data;
using TransferService.Entities;

namespace TransferService.Messaging;

public sealed class TransferWorkflowConsumer(
    TransferDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IPublishEndpoint publishEndpoint,
    ILogger<TransferWorkflowConsumer> logger) : IConsumer<StartTransferCommand>
{
    public async Task Consume(ConsumeContext<StartTransferCommand> context)
    {
        var transfer = await db.Transfers.SingleOrDefaultAsync(x => x.Id == context.Message.TransferId, context.CancellationToken);
        if (transfer is null || transfer.Status is "Completed" or "Compensated") return;

        using var scope = logger.BeginScope(new Dictionary<string, object> { ["TransferId"] = transfer.Id });
        transfer.Status = "Processing";
        await db.SaveChangesAsync(context.CancellationToken);

        try
        {
            await ExecuteStepAsync(transfer, "DebitSender", "debit", transfer.SenderUserId, context);
            await ExecuteStepAsync(transfer, "CreditReceiver", "credit", transfer.ReceiverUserId, context);
            transfer.Status = "Completed";
            transfer.CompletedAtUtc = DateTime.UtcNow;
            await publishEndpoint.Publish(new TransferCompletedEvent(
                transfer.Id,
                transfer.SenderUserId,
                transfer.ReceiverUserId,
                transfer.Amount,
                transfer.Currency,
                transfer.CompletedAtUtc.Value), context.CancellationToken);
            await db.SaveChangesAsync(context.CancellationToken);
            logger.LogInformation("Transfer {TransferId} completed", transfer.Id);
        }
        catch (Exception ex)
        {
            transfer.Status = "Compensating";
            transfer.FailureReason = ex.Message[..Math.Min(500, ex.Message.Length)];
            await db.SaveChangesAsync(context.CancellationToken);
            await CompensateAsync(transfer, context);
            throw;
        }
    }

    private async Task ExecuteStepAsync(Transfer transfer, string type, string operation, Guid userId, ConsumeContext context)
    {
        var step = await db.TransferSteps.SingleOrDefaultAsync(x => x.TransferId == transfer.Id && x.StepType == type, context.CancellationToken);
        if (step?.Status == "Completed") return;
        step ??= new TransferStep { TransferId = transfer.Id, StepType = type };
        if (step.Id == default) db.TransferSteps.Add(step);
        step.Status = "Processing";
        await db.SaveChangesAsync(context.CancellationToken);

        var client = httpClientFactory.CreateClient("wallet");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/wallet/internal/{operation}");
        request.Headers.Add("X-Internal-Api-Key", configuration["Internal:ApiKey"] ?? throw new InvalidOperationException("Internal API key is missing."));
        request.Headers.Add("X-Transfer-Id", transfer.Id.ToString());
        request.Headers.Add("X-Step-Id", step.Id.ToString());
        request.Headers.Add("X-Operation-Id", step.Id.ToString());
        request.Content = JsonContent.Create(new { userId, amount = transfer.Amount });
        using var response = await client.SendAsync(request, context.CancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Wallet {operation} failed: {await response.Content.ReadAsStringAsync(context.CancellationToken)}");
        step.Status = "Completed";
        step.CompletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(context.CancellationToken);
    }

    private async Task CompensateAsync(Transfer transfer, ConsumeContext context)
    {
        var credited = await db.TransferSteps.AnyAsync(x => x.TransferId == transfer.Id && x.StepType == "CreditReceiver" && x.Status == "Completed", context.CancellationToken);
        var debited = await db.TransferSteps.AnyAsync(x => x.TransferId == transfer.Id && x.StepType == "DebitSender" && x.Status == "Completed", context.CancellationToken);
        try
        {
            if (credited) await WalletAsync("debit", transfer.ReceiverUserId, transfer, context);
            if (debited) await WalletAsync("credit", transfer.SenderUserId, transfer, context);
            transfer.Status = "Failed";
            transfer.FailedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(context.CancellationToken);
        }
        catch (Exception compensationEx)
        {
            transfer.Status = "CompensationFailed";
            transfer.FailureReason = compensationEx.Message[..Math.Min(500, compensationEx.Message.Length)];
            await db.SaveChangesAsync(context.CancellationToken);
            logger.LogCritical(compensationEx, "Compensation failed for transfer {TransferId}", transfer.Id);
        }
    }

    private async Task WalletAsync(string operation, Guid userId, Transfer transfer, ConsumeContext context)
    {
        var client = httpClientFactory.CreateClient("wallet");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/wallet/internal/{operation}");
        request.Headers.Add("X-Internal-Api-Key", configuration["Internal:ApiKey"]!);
        request.Headers.Add("X-Transfer-Id", transfer.Id.ToString());
        request.Headers.Add("X-Compensation", "true");
        request.Headers.Add("X-Operation-Id", $"{transfer.Id}:compensate:{operation}:{userId}");
        request.Content = JsonContent.Create(new { userId, amount = transfer.Amount });
        using var response = await client.SendAsync(request, context.CancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await response.Content.ReadAsStringAsync(context.CancellationToken));
    }
}
