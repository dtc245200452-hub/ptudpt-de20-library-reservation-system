using System.Text.Json;
using Library.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Library.WebClient.Pages;

public class UserReservationsModel : PageModel
{
    public static readonly Guid SeedUser1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SeedUser2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UserReservationsModel> _logger;

    public UserReservationsModel(IHttpClientFactory httpClientFactory, ILogger<UserReservationsModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public Guid SelectedUserId { get; set; } = SeedUser1Id;

    public List<ReservationDto> UserReservations { get; set; } = new();

    public string UserName { get; set; } = "Nguyễn Văn An";

    public async Task OnGetAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("LibraryApi");
            var url = $"/users/{SelectedUserId}/reservations";

            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                UserReservations = JsonSerializer.Deserialize<List<ReservationDto>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new();
            }

            UserName = SelectedUserId == SeedUser1Id ? "Nguyễn Văn An (B22DCCN001)" : "Trần Thị Bình (B22DCCN002)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user reservations");
        }
    }
}
