using Library.Contracts.Enums;

namespace Library.Contracts.DTOs;

public record CreateReservationRequest(
    Guid BookId,
    Guid UserId
);

public record ReservationDto(
    Guid Id,
    Guid BookId,
    string? BookTitle,
    Guid UserId,
    string? UserName,
    ReservationStatus Status,
    string? RejectReason,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    DateTime? BorrowedAt,
    DateTime? ReturnedAt
);

public record ReservationStatusResponse(
    Guid Id,
    ReservationStatus Status,
    string? Message = null
);
