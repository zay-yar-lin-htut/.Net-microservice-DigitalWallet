namespace TransferService.Services;

public sealed class SameUserTransferException()
    : Exception("Sender and receiver must be different.");

public sealed class TransferOperationException(string message)
    : Exception(message);
