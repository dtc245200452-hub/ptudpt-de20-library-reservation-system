using Library.Contracts.Enums;
using Library.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Data;

public class LibraryDbContext : DbContext
{
    // Fixed seed data GUIDs for easy testing and API calls
    public static readonly Guid SeedUser1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SeedUser2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly Guid SeedBook1Id = Guid.Parse("bbbbbbbb-0001-0000-0000-000000000001");
    public static readonly Guid SeedBook2Id = Guid.Parse("bbbbbbbb-0002-0000-0000-000000000002");
    public static readonly Guid SeedBook3Id = Guid.Parse("bbbbbbbb-0003-0000-0000-000000000003");
    public static readonly Guid SeedBook4Id = Guid.Parse("bbbbbbbb-0004-0000-0000-000000000004");
    public static readonly Guid SeedBook5Id = Guid.Parse("bbbbbbbb-0005-0000-0000-000000000005");

    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
    {
    }

    public DbSet<Book> Books => Set<Book>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Book configuration
        modelBuilder.Entity<Book>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).IsRequired().HasMaxLength(250);
            b.Property(x => x.Author).IsRequired().HasMaxLength(200);
            b.Property(x => x.Isbn).HasMaxLength(50);
            b.Property(x => x.Category).HasMaxLength(100);
            b.HasIndex(x => x.Isbn).IsUnique();
        });

        // User configuration
        modelBuilder.Entity<User>(u =>
        {
            u.HasKey(x => x.Id);
            u.Property(x => x.FullName).IsRequired().HasMaxLength(150);
            u.Property(x => x.Email).IsRequired().HasMaxLength(150);
            u.Property(x => x.StudentCode).IsRequired().HasMaxLength(50);
            u.HasIndex(x => x.StudentCode).IsUnique();
        });

        // Reservation configuration
        modelBuilder.Entity<Reservation>(r =>
        {
            r.HasKey(x => x.Id);
            r.Property(x => x.Status).HasConversion<int>();
            r.Property(x => x.RejectReason).HasMaxLength(500);
            r.Property(x => x.CorrelationId).HasMaxLength(100);

            r.HasOne(x => x.Book)
                .WithMany(b => b.Reservations)
                .HasForeignKey(x => x.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            r.HasOne(x => x.User)
                .WithMany(u => u.Reservations)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            r.HasIndex(x => x.Status);
            r.HasIndex(x => x.UserId);
        });

        // Seed initial data
        SeedInitialData(modelBuilder);
    }

    private static void SeedInitialData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = SeedUser1Id,
                FullName = "Nguyễn Văn An",
                Email = "an.nguyen@university.edu.vn",
                StudentCode = "B22DCCN001"
            },
            new User
            {
                Id = SeedUser2Id,
                FullName = "Trần Thị Bình",
                Email = "binh.tran@university.edu.vn",
                StudentCode = "B22DCCN002"
            }
        );

        modelBuilder.Entity<Book>().HasData(
            new Book
            {
                Id = SeedBook1Id,
                Title = "Designing Data-Intensive Applications",
                Author = "Martin Kleppmann",
                Isbn = "978-1449373320",
                Category = "Distributed Systems",
                Description = "Cẩm nang toàn diện về hệ thống phân tán, xử lý dữ liệu và lưu trữ tin cậy.",
                TotalCopies = 3,
                AvailableCopies = 3
            },
            new Book
            {
                Id = SeedBook2Id,
                Title = "Clean Architecture: A Craftsman's Guide",
                Author = "Robert C. Martin",
                Isbn = "978-0134494164",
                Category = "Software Engineering",
                Description = "Nguyên lý kiến trúc phần mềm sạch và thiết kế hướng đối tượng chuyên sâu.",
                TotalCopies = 2,
                AvailableCopies = 2
            },
            new Book
            {
                Id = SeedBook3Id,
                Title = "The Pragmatic Programmer (20th Anniversary)",
                Author = "David Thomas, Andrew Hunt",
                Isbn = "978-0135957059",
                Category = "Software Engineering",
                Description = "Hành trình từ thợ code đến nghệ nhân phần mềm hiện đại.",
                TotalCopies = 1, // Cuốn này chỉ có 1 bản duy nhất để test Race Condition!
                AvailableCopies = 1
            },
            new Book
            {
                Id = SeedBook4Id,
                Title = "Building Microservices: Designing Fine-Grained Systems",
                Author = "Sam Newman",
                Isbn = "978-1492034025",
                Category = "Architecture",
                Description = "Hướng dẫn thực chiến xây dựng, bảo trì và tích hợp các microservice.",
                TotalCopies = 4,
                AvailableCopies = 4
            },
            new Book
            {
                Id = SeedBook5Id,
                Title = "Database System Concepts (7th Edition)",
                Author = "Abraham Silberschatz, Henry F. Korth",
                Isbn = "978-0078022159",
                Category = "Database",
                Description = "Giáo trình kinh điển về hệ cơ sở dữ liệu và quản trị giao dịch phân tán.",
                TotalCopies = 2,
                AvailableCopies = 2
            }
        );
    }
}
