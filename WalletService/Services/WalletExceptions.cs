namespace WalletService.Services;

public sealed class WalletNotFoundException()
    : Exception("Wallet not found.");

public sealed class WalletAlreadyExistsException()
    : Exception("Wallet already exists.");

public sealed class InsufficientBalanceException()
    : Exception("Insufficient balance.");
