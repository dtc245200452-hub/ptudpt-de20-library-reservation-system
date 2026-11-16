using Library.Contracts.Events;

namespace Library.Api.Services;

public interface IRabbitMqPublisher
{
    Task PublishReservationCreatedAsync(ReservationCreatedEvent evt, CancellationToken ct = default);
}
