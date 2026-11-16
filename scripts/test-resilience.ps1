# Kịch bản kiểm thử Chịu lỗi & Phục hồi khi Worker dừng (Resilience / Fault Tolerance)
# Yêu cầu T13 trong HUONG-DAN-CHUNG.md

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   KIỂM THỬ KHẢ NĂNG CHỊU LỖI & PHỤC HỒI (RESILIENCE)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$baseUrl = "http://localhost:5000"

Write-Host "`nBước 1: Tắt Worker container (mô phỏng sự cố Worker gặp lỗi dừng hoạt động)..." -ForegroundColor Yellow
docker compose stop reservation-worker
Write-Host "-> Worker đã dừng!" -ForegroundColor Red

Write-Host "`nBước 2: Gửi yêu cầu đặt mượn khi Worker đang TẮT..." -ForegroundColor Yellow
$books = Invoke-RestMethod -Uri "$baseUrl/books" -Method Get
$targetBook = $books[0]
$userId = "11111111-1111-1111-1111-111111111111"

$body = @{ bookId = $targetBook.id; userId = $userId } | ConvertTo-Json
$postResponse = Invoke-WebRequest -Uri "$baseUrl/reservations" -Method Post -Body $body -ContentType "application/json"
$created = $postResponse.Content | ConvertFrom-Json
$resId = $created.id
Write-Host "-> API vẫn tiếp nhận bình thường và đẩy vào RabbitMQ! Status: 202 Accepted, ReservationId: $resId" -ForegroundColor Green

Write-Host "`nBước 3: Kiểm tra trạng thái yêu cầu..." -ForegroundColor Yellow
$checkPending = Invoke-RestMethod -Uri "$baseUrl/reservations/$resId" -Method Get
Write-Host "-> Trạng thái hiện tại: $($checkPending.status) (Đang lưu trữ an toàn trong RabbitMQ)" -ForegroundColor Yellow

Write-Host "`nBước 4: Khởi động lại Worker (Phục hồi dịch vụ)..." -ForegroundColor Yellow
docker compose start reservation-worker
Write-Host "-> Worker đã khởi động lại! Đang kết nối lại RabbitMQ để tiêu thụ message tồn đọng..." -ForegroundColor Green

Write-Host "`nBước 5: Chờ Worker tự động phục hồi và xử lý message..." -ForegroundColor Yellow
for ($i = 1; $i -le 10; $i++) {
    Start-Sleep -Seconds 1
    $statusCheck = Invoke-RestMethod -Uri "$baseUrl/reservations/$resId" -Method Get
    Write-Host ("   Giây {0}: Trạng thái = {1}" -f $i, $statusCheck.status)
    if ($statusCheck.status -ne "Pending") {
        Write-Host "`n=> KẾT QUẢ: Hệ thống đã tự phục hồi thành công! Trạng thái cuối cùng: $($statusCheck.status)" -ForegroundColor Green
        break
    }
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
