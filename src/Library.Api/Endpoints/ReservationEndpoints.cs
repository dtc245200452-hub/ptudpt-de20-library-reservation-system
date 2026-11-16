using Library.Api.Middleware;
using Library.Api.Services;
using Library.Contracts.DTOs;
using Library.Contracts.Enums;
using Library.Contracts.Events;
using Library.Data;
using Library.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Endpoints;

public static class ReservationEndpoints
{
    public static RouteGroupBuilder MapReservationEndpoints(this RouteGroupBuilder group)
    {
        // POST /reservations - Gửi yêu cầu đặt mượn sách (Xử lý bất đồng bộ qua RabbitMQ)
        group.MapPost("/", async (
            CreateReservationRequest request,
            LibraryDbContext db,
            IRabbitMqPublisher publisher,
            HttpContext httpContext,
            ILogger<Program> logger) =>
        {
            var correlationId = httpContext.Items[CorrelationIdMiddleware.HeaderKey]?.ToString();
            logger.LogInformation("[CorrelationId: {CorrelationId}] POST /reservations: User {UserId} requesting Book {BookId}",
                correlationId, request.UserId, request.BookId);

            // Kiểm tra sách và người dùng có tồn tại không
            var book = await db.Books.FindAsync(request.BookId);
            if (book is null)
            {
                return Results.NotFound(new { message = $"Không tìm thấy sách với mã: {request.BookId}" });
            }

            var user = await db.Users.FindAsync(request.UserId);
            if (user is null)
            {
                return Results.NotFound(new { message = $"Không tìm thấy người dùng với mã: {request.UserId}" });
            }

            // Tạo bản ghi Reservation ban đầu ở trạng thái Pending
            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                BookId = request.BookId,
                UserId = request.UserId,
                Status = ReservationStatus.Pending,
                CorrelationId = correlationId,
                CreatedAt = DateTime.UtcNow
            };

            db.Reservations.Add(reservation);
            await db.SaveChangesAsync();

            logger.LogInformation(
                "[CorrelationId: {CorrelationId}] Reservation {ReservationId} saved with status PENDING",
                correlationId, reservation.Id);

            // Publish message vào RabbitMQ
            var evt = new ReservationCreatedEvent(
                ReservationId: reservation.Id,
                BookId: reservation.BookId,
                UserId: reservation.UserId,
                CorrelationId: correlationId,
                CreatedAtUtc: reservation.CreatedAt
            );

            await publisher.PublishReservationCreatedAsync(evt);

            // Trả về 202 Accepted vì hệ thống tiếp nhận và xử lý bất đồng bộ
            var response = new ReservationStatusResponse(
                reservation.Id,
                reservation.Status,
                "Yêu cầu đặt mượn đã được tiếp nhận và đưa vào hàng đợi xử lý bất đồng bộ."
            );

            return Results.Accepted($"/reservations/{reservation.Id}", response);
        })
        .WithName("CreateReservation")
        .WithSummary("Gửi yêu cầu đặt mượn sách (bất đồng bộ qua RabbitMQ)")
        .Produces<ReservationStatusResponse>(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status404NotFound);

        // GET /reservations/{id} - Tra cứu trạng thái yêu cầu đặt mượn
        group.MapGet("/{id:guid}", async (Guid id, LibraryDbContext db, ILogger<Program> logger) =>
        {
            logger.LogInformation("Handling GET /reservations/{Id}", id);

            var r = await db.Reservations
                .AsNoTracking()
                .Include(x => x.Book)
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (r is null)
            {
                return Results.NotFound(new { message = $"Không tìm thấy yêu cầu đặt mượn với mã: {id}" });
            }

            var dto = new ReservationDto(
                r.Id,
                r.BookId,
                r.Book?.Title,
                r.UserId,
                r.User?.FullName,
                r.Status,
                r.RejectReason,
                r.CreatedAt,
                r.ProcessedAt,
                r.BorrowedAt,
                r.ReturnedAt
            );

            return Results.Ok(dto);
        })
        .WithName("GetReservationById")
        .WithSummary("Tra cứu trạng thái yêu cầu đặt mượn")
        .Produces<ReservationDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // PUT /reservations/{id}/borrow - Thủ thư xác nhận giao sách (Reserved -> Borrowed)
        group.MapPut("/{id:guid}/borrow", async (Guid id, LibraryDbContext db, ILogger<Program> logger) =>
        {
            logger.LogInformation("Handling PUT /reservations/{Id}/borrow", id);

            var reservation = await db.Reservations.FindAsync(id);
            if (reservation is null)
            {
                return Results.NotFound(new { message = $"Không tìm thấy yêu cầu đặt mượn: {id}" });
            }

            if (reservation.Status != ReservationStatus.Reserved)
            {
                return Results.BadRequest(new
                {
                    message = $"Chỉ có thể chuyển sang 'Borrowed' khi yêu cầu ở trạng thái 'Reserved'. Trạng thái hiện tại: {reservation.Status}"
                });
            }

            reservation.Status = ReservationStatus.Borrowed;
            reservation.BorrowedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            logger.LogInformation("Reservation {Id} updated to BORROWED", id);
            return Results.Ok(new ReservationStatusResponse(reservation.Id, reservation.Status, "Đã giao sách cho độc giả thành công."));
        })
        .WithName("BorrowReservation")
        .WithSummary("Thủ thư xác nhận giao sách cho độc giả (Reserved -> Borrowed)")
        .Produces<ReservationStatusResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        // PUT /reservations/{id}/return - Độc giả trả sách (Borrowed -> Returned, hoàn lại AvailableCopies)
        group.MapPut("/{id:guid}/return", async (Guid id, LibraryDbContext db, ILogger<Program> logger) =>
        {
            logger.LogInformation("Handling PUT /reservations/{Id}/return", id);

            var reservation = await db.Reservations.Include(r => r.Book).FirstOrDefaultAsync(r => r.Id == id);
            if (reservation is null)
            {
                return Results.NotFound(new { message = $"Không tìm thấy yêu cầu đặt mượn: {id}" });
            }

            if (reservation.Status != ReservationStatus.Borrowed)
            {
                return Results.BadRequest(new
                {
                    message = $"Chỉ có thể chuyển sang 'Returned' khi yêu cầu ở trạng thái 'Borrowed'. Trạng thái hiện tại: {reservation.Status}"
                });
            }

            reservation.Status = ReservationStatus.Returned;
            reservation.ReturnedAt = DateTime.UtcNow;

            // Hoàn lại 1 bản sách khả dụng
            if (reservation.Book is not null)
            {
                reservation.Book.AvailableCopies = Math.Min(reservation.Book.TotalCopies, reservation.Book.AvailableCopies + 1);
            }

            await db.SaveChangesAsync();

            logger.LogInformation("Reservation {Id} updated to RETURNED. Book {BookId} AvailableCopies increased.", id, reservation.BookId);
            return Results.Ok(new ReservationStatusResponse(reservation.Id, reservation.Status, "Đã ghi nhận trả sách và cập nhật số bản khả dụng."));
        })
        .WithName("ReturnReservation")
        .WithSummary("Độc giả trả sách (Borrowed -> Returned, hoàn lại số bản khả dụng)")
        .Produces<ReservationStatusResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
    {
        // GET /users/{userId}/reservations - Tra cứu danh sách sách đang mượn hoặc đã đặt của một người dùng
        group.MapGet("/{userId:guid}/reservations", async (Guid userId, LibraryDbContext db, ILogger<Program> logger) =>
        {
            logger.LogInformation("Handling GET /users/{UserId}/reservations", userId);

            var userExists = await db.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                return Results.NotFound(new { message = $"Không tìm thấy người dùng với mã: {userId}" });
            }

            var list = await db.Reservations
                .AsNoTracking()
                .Include(r => r.Book)
                .Include(r => r.User)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReservationDto(
                    r.Id,
                    r.BookId,
                    r.Book != null ? r.Book.Title : null,
                    r.UserId,
                    r.User != null ? r.User.FullName : null,
                    r.Status,
                    r.RejectReason,
                    r.CreatedAt,
                    r.ProcessedAt,
                    r.BorrowedAt,
                    r.ReturnedAt
                ))
                .ToListAsync();

            return Results.Ok(list);
        })
        .WithName("GetUserReservations")
        .WithSummary("Tra cứu danh sách sách đang mượn hoặc đã đặt của một người dùng")
        .Produces<List<ReservationDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return group;
    }
}
