namespace Library.Contracts.DTOs;

public record BookListDto(
    Guid Id,
    string Title,
    string Author,
    string? Isbn,
    string? Category,
    int TotalCopies,
    int AvailableCopies
);

public record BookDetailDto(
    Guid Id,
    string Title,
    string Author,
    string? Isbn,
    string? Category,
    string? Description,
    int TotalCopies,
    int AvailableCopies
);
