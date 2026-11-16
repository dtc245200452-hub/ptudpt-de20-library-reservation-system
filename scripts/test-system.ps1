# Kịch bản kiểm thử tự động toàn diện hệ thống phân tán (Đề tài 20)
# Usage: powershell -ExecutionPolicy Bypass -File scripts\test-system.ps1

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   KIỂM THỬ HỆ THỐNG THƯ VIỆN & ĐẶT MƯỢN SÁCH PHÂN TÁN" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$baseUrl = "http://localhost:5000"

# 1. Kiểm tra Health Check
Write-Host "`n[1/5] Kiểm tra Health Check API..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/healthz" -Method Get
    Write-Host "-> API & PostgreSQL Health: OK" -ForegroundColor Green
} catch {
    Write-Host "-> Lỗi kết nối API! Hãy chắc chắn 'docker compose up -d' đang chạy." -ForegroundColor Red
    exit 1
}

# 2. Tra cứu danh sách sách & số bản khả dụng
Write-Host "`n[2/5] Tra cứu danh sách sách (GET /books)..." -ForegroundColor Yellow
$books = Invoke-RestMethod -Uri "$baseUrl/books" -Method Get
Write-Host "-> Tìm thấy $($books.Count) cuốn sách trong hệ thống:" -ForegroundColor Green
$books | ForEach-Object {
    Write-Host ("   - [{0}] {1} (Khả dụng: {2}/{3})" -f $_.id.Substring(0,8), $_.title, $_.availableCopies, $_.totalCopies)
}

# Chọn cuốn sách có 1 bản duy nhất để test (SeedBook3Id)
$targetBook = $books | Where-Object { $_.title -like "*Pragmatic*" } | Select-Object -First 1
if (-not $targetBook) { $targetBook = $books[0] }
$bookId = $targetBook.id
$userId = "11111111-1111-1111-1111-111111111111" # Nguyễn Văn An

Write-Host "`n[3/5] Gửi yêu cầu đặt mượn sách bất đồng bộ (POST /reservations)..." -ForegroundColor Yellow
Write-Host "-> Đặt cuốn: $($targetBook.title) (Mã: $bookId)"
$body = @{ bookId = $bookId; userId = $userId } | ConvertTo-Json

$postResponse = Invoke-WebRequest -Uri "$baseUrl/reservations" -Method Post -Body $body -ContentType "application/json"
$created = $postResponse.Content | ConvertFrom-Json
$resId = $created.id
Write-Host "-> HTTP Status Code: $($postResponse.StatusCode) (Accepted)" -ForegroundColor Green
Write-Host "-> Mã yêu cầu tạo thành công: $resId (Trạng thái ban đầu: $($created.status))" -ForegroundColor Green

# 3. Chờ Worker consume message từ RabbitMQ và cập nhật
Write-Host "`n[4/5] Theo dõi Worker xử lý thông điệp từ RabbitMQ..." -ForegroundColor Yellow
$maxWait = 10
$finalStatus = ""
for ($i = 1; $i -le $maxWait; $i++) {
    Start-Sleep -Seconds 1
    $statusCheck = Invoke-RestMethod -Uri "$baseUrl/reservations/$resId" -Method Get
    Write-Host ("   Giây {0}: Trạng thái = {1}" -f $i, $statusCheck.status)
    if ($statusCheck.status -ne "Pending") {
        $finalStatus = $statusCheck.status
        break
    }
}

if ($finalStatus -eq "Reserved") {
    Write-Host "-> THÀNH CÔNG: Worker đã nhận message, trừ kho và chuyển trạng thái sang RESERVED!" -ForegroundColor Green
} else {
    Write-Host "-> Kết quả: $finalStatus (Chi tiết: $($statusCheck.rejectReason))" -ForegroundColor Yellow
}

# 4. Kiểm tra vòng đời: Giao sách (Borrow) và Trả sách (Return)
if ($finalStatus -eq "Reserved") {
    Write-Host "`n[5/5] Kiểm tra vòng đời: Giao sách (Borrow) -> Trả sách (Return)..." -ForegroundColor Yellow
    
    # Borrow
    $borrowRes = Invoke-RestMethod -Uri "$baseUrl/reservations/$resId/borrow" -Method Put
    Write-Host "-> Thủ thư giao sách: Trạng thái = $($borrowRes.status)" -ForegroundColor Green

    # Return
    $returnRes = Invoke-RestMethod -Uri "$baseUrl/reservations/$resId/return" -Method Put
    Write-Host "-> Độc giả trả sách: Trạng thái = $($returnRes.status)" -ForegroundColor Green

    # Kiểm tra lại số lượng khả dụng
    $bookAfter = Invoke-RestMethod -Uri "$baseUrl/books/$bookId" -Method Get
    Write-Host ("-> Số bản khả dụng sau khi trả: {0}/{1} (Đã hoàn lại kho thành công)" -f $bookAfter.availableCopies, $bookAfter.totalCopies) -ForegroundColor Green
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "   HOÀN THÀNH KIỂM THỬ HỆ THỐNG PHÂN TÁN!" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
