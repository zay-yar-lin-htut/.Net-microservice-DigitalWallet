using TransferService.Events;

namespace TransferService.Messaging;

public interface IEventPublisher
{
    Task PublishTransferCompletedAsync(
        TransferCompletedEvent message,
        CancellationToken cancellationToken);
}
