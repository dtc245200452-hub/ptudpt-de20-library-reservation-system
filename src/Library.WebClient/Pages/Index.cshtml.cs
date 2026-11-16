using System.Text;
using System.Text.Json;
using Library.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Library.WebClient.Pages;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public List<BookListDto> Books { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SelectedCategory { get; set; }

    [TempData]
    public string? AlertMessage { get; set; }

    [TempData]
    public string? AlertType { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("LibraryApi");
            var url = "/books";
            var query = new List<string>();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
                query.Add($"search={Uri.EscapeDataString(SearchTerm)}");

            if (!string.IsNullOrWhiteSpace(SelectedCategory))
                query.Add($"category={Uri.EscapeDataString(SelectedCategory)}");

            if (query.Count > 0)
                url += "?" + string.Join("&", query);

            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Books = JsonSerializer.Deserialize<List<BookListDto>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching books from LibraryApi");
            AlertMessage = "Không thể kết nối tới Library API. Hãy đảm bảo container library-api đang chạy.";
            AlertType = "danger";
        }
    }

    public async Task<IActionResult> OnPostReserveAsync(Guid bookId, Guid userId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("LibraryApi");
            var payload = new CreateReservationRequest(bookId, userId);
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/reservations", content);
            if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ReservationStatusResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                AlertMessage = $"Yêu cầu đặt mượn đã gửi thành công vào RabbitMQ! Mã đặt: {result?.Id}. Đang được Worker xử lý...";
                AlertType = "success";
                return RedirectToPage("/Reservations");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                AlertMessage = $"Không thể đặt sách: {error}";
                AlertType = "danger";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting reservation");
            AlertMessage = $"Lỗi kết nối khi gửi yêu cầu đặt mượn: {ex.Message}";
            AlertType = "danger";
        }

        return RedirectToPage();
    }
}
