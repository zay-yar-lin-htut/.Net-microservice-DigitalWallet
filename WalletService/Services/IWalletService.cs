using WalletService.Entities;
using WalletService.Contracts;

namespace WalletService.Services;

public interface IWalletService
{
    Task<Wallet> CreateAsync(Guid userId, CancellationToken cancellationToken);

    Task<Wallet?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Wallet> DepositAsync(
        Guid userId,
        decimal amount,
        CancellationToken cancellationToken);

    Task<Wallet> WithdrawAsync(
        Guid userId,
        decimal amount,
        CancellationToken cancellationToken);

    Task<Wallet> CreditInternalAsync(
        Guid userId,
        decimal amount,
        string? referenceId,
        CancellationToken cancellationToken);

    Task<Wallet> DebitInternalAsync(
        Guid userId,
        decimal amount,
        string? referenceId,
        CancellationToken cancellationToken);

    Task<WalletOperationResponse> CreateOperationAsync(
        Guid userId,
        string operationType,
        decimal amount,
        CancellationToken cancellationToken);

    Task<WalletOperationResponse?> GetOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken);
}
