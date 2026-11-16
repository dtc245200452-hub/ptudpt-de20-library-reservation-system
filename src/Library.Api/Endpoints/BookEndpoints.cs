using Library.Contracts.DTOs;
using Library.Data;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Endpoints;

public static class BookEndpoints
{
    public static RouteGroupBuilder MapBookEndpoints(this RouteGroupBuilder group)
    {
        // GET /books - Tra cứu danh sách sách (hỗ trợ search và category)
        group.MapGet("/", async (LibraryDbContext db, string? search, string? category, ILogger<Program> logger) =>
        {
            logger.LogInformation("Handling GET /books with search='{Search}', category='{Category}'", search, category);

            var query = db.Books.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(b => b.Title.ToLower().Contains(term) || b.Author.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                var cat = category.Trim().ToLower();
                query = query.Where(b => b.Category != null && b.Category.ToLower() == cat);
            }

            var books = await query
                .OrderBy(b => b.Title)
                .Select(b => new BookListDto(
                    b.Id,
                    b.Title,
                    b.Author,
                    b.Isbn,
                    b.Category,
                    b.TotalCopies,
                    b.AvailableCopies
                ))
                .ToListAsync();

            return Results.Ok(books);
        })
        .WithName("GetBooks")
        .WithSummary("Tra cứu danh sách sách và số bản khả dụng")
        .Produces<List<BookListDto>>(StatusCodes.Status200OK);

        // GET /books/{id} - Tra cứu chi tiết sách
        group.MapGet("/{id:guid}", async (Guid id, LibraryDbContext db, ILogger<Program> logger) =>
        {
            logger.LogInformation("Handling GET /books/{Id}", id);

            var book = await db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
            if (book is null)
            {
                logger.LogWarning("Book {Id} not found", id);
                return Results.NotFound(new { message = $"Không tìm thấy sách với mã: {id}" });
            }

            var dto = new BookDetailDto(
                book.Id,
                book.Title,
                book.Author,
                book.Isbn,
                book.Category,
                book.Description,
                book.TotalCopies,
                book.AvailableCopies
            );

            return Results.Ok(dto);
        })
        .WithName("GetBookById")
        .WithSummary("Tra cứu chi tiết một cuốn sách")
        .Produces<BookDetailDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return group;
    }
}
