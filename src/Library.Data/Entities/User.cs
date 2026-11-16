namespace Library.Data.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;

    // Navigation property
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
