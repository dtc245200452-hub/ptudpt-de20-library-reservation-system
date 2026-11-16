using System.Text.Json;
using Library.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Library.WebClient.Pages;

public class ReservationsModel : PageModel
{
    // Fixed Seed User IDs
    public static readonly Guid SeedUser1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SeedUser2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReservationsModel> _logger;

    public ReservationsModel(IHttpClientFactory httpClientFactory, ILogger<ReservationsModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public List<ReservationDto> Reservations { get; set; } = new();

    [TempData]
    public string? AlertMessage { get; set; }

    [TempData]
    public string? AlertType { get; set; }

    public async Task OnGetAsync()
    {
        await LoadReservationsAsync();
    }

    private async Task LoadReservationsAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("LibraryApi");

            // Lấy danh sách yêu cầu của cả 2 user mẫu để tổng hợp
            var user1Url = $"/users/{SeedUser1Id}/reservations";
            var user2Url = $"/users/{SeedUser2Id}/reservations";

            var list = new List<ReservationDto>();

            var res1 = await client.GetAsync(user1Url);
            if (res1.IsSuccessStatusCode)
            {
                var json1 = await res1.Content.ReadAsStringAsync();
                var items1 = JsonSerializer.Deserialize<List<ReservationDto>>(json1, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (items1 != null) list.AddRange(items1);
            }

            var res2 = await client.GetAsync(user2Url);
            if (res2.IsSuccessStatusCode)
            {
                var json2 = await res2.Content.ReadAsStringAsync();
                var items2 = JsonSerializer.Deserialize<List<ReservationDto>>(json2, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (items2 != null) list.AddRange(items2);
            }

            Reservations = list.OrderByDescending(r => r.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading reservations");
            AlertMessage = "Không thể tải danh sách đặt mượn từ Library API.";
            AlertType = "danger";
        }
    }

    public async Task<IActionResult> OnPostBorrowAsync(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("LibraryApi");
            var response = await client.PutAsync($"/reservations/{id}/borrow", null);
            if (response.IsSuccessStatusCode)
            {
                AlertMessage = $"Đã cập nhật trạng thái: Độc giả đã nhận mượn sách thành công!";
                AlertType = "success";
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                AlertMessage = $"Lỗi cập nhật mượn sách: {err}";
                AlertType = "danger";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating borrow status");
            AlertMessage = $"Lỗi: {ex.Message}";
            AlertType = "danger";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReturnAsync(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("LibraryApi");
            var response = await client.PutAsync($"/reservations/{id}/return", null);
            if (response.IsSuccessStatusCode)
            {
                AlertMessage = $"Đã cập nhật hoàn trả sách và hoàn lại số lượng khả dụng vào kho!";
                AlertType = "success";
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                AlertMessage = $"Lỗi cập nhật trả sách: {err}";
                AlertType = "danger";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating return status");
            AlertMessage = $"Lỗi: {ex.Message}";
            AlertType = "danger";
        }

        return RedirectToPage();
    }
}
