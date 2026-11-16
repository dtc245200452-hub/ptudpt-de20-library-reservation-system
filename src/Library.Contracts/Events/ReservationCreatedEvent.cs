namespace Library.Contracts.Events;

public record ReservationCreatedEvent(
    Guid ReservationId,
    Guid BookId,
    Guid UserId,
    string? CorrelationId = null,
    DateTime CreatedAtUtc = default
);
