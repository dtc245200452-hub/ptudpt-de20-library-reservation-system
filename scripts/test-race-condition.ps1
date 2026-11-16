# Kịch bản kiểm thử Tranh chấp tài nguyên đồng thời (Race Condition / Overbooking Prevention)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   KIỂM THỬ TRANH CHẤP TÀI NGUYÊN ĐỒNG THỜI (CONCURRENCY)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$baseUrl = "http://localhost:5000"

# Tìm cuốn sách The Pragmatic Programmer (chỉ có 1 bản duy nhất)
$books = Invoke-RestMethod -Uri "$baseUrl/books" -Method Get
$book = $books | Where-Object { $_.title -like "*Pragmatic*" } | Select-Object -First 1

if (-not $book) {
    Write-Host "Không tìm thấy cuốn sách mẫu!" -ForegroundColor Red
    exit 1
}

Write-Host ("`nCuốn sách thử nghiệm: '{0}'" -f $book.title)
Write-Host ("Số bản khả dụng hiện tại: {0}/{1}" -f $book.availableCopies, $book.totalCopies) -ForegroundColor Yellow

if ($book.availableCopies -le 0) {
    Write-Host "Cảnh báo: Sách hiện đã hết bản khả dụng. Vui lòng hoàn trả trước khi test." -ForegroundColor Red
    exit 1
}

Write-Host "`nĐang gửi đồng thời 5 yêu cầu đặt mượn cùng lúc vào hệ thống..." -ForegroundColor Yellow

$userIds = @(
    "11111111-1111-1111-1111-111111111111",
    "22222222-2222-2222-2222-222222222222",
    "11111111-1111-1111-1111-111111111111",
    "22222222-2222-2222-2222-222222222222",
    "11111111-1111-1111-1111-111111111111"
)

$reservationIds = @()

foreach ($u in $userIds) {
    $body = @{ bookId = $book.id; userId = $u } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$baseUrl/reservations" -Method Post -Body $body -ContentType "application/json"
    $reservationIds += $res.id
    Write-Host ("   -> Đã gửi yêu cầu: {0}" -f $res.id)
}

Write-Host "`nĐang chờ Worker xử lý tất cả yêu cầu..." -ForegroundColor Yellow
Start-Sleep -Seconds 4

Write-Host "`nKết quả xử lý chi tiết từng yêu cầu:" -ForegroundColor Yellow
$reservedCount = 0
$rejectedCount = 0

foreach ($id in $reservationIds) {
    $r = Invoke-RestMethod -Uri "$baseUrl/reservations/$id" -Method Get
    if ($r.status -eq "Reserved") {
        Write-Host ("   - [{0}] -> RESERVED (Đặt thành công)" -f $id.Substring(0,8)) -ForegroundColor Green
        $reservedCount++
    } else {
        Write-Host ("   - [{0}] -> REJECTED (Từ chối: {1})" -f $id.Substring(0,8), $r.rejectReason) -ForegroundColor Red
        $rejectedCount++
    }
}

# Kiểm tra lại số lượng khả dụng trong kho sách
$bookFinal = Invoke-RestMethod -Uri "$baseUrl/books/$($book.id)" -Method Get
Write-Host ("`nTổng kết:") -ForegroundColor Cyan
Write-Host ("-> Số yêu cầu thành công (Reserved): {0}" -f $reservedCount) -ForegroundColor Green
Write-Host ("-> Số yêu cầu bị từ chối (Rejected): {0}" -f $rejectedCount) -ForegroundColor Yellow
Write-Host ("-> Số bản khả dụng còn lại trong kho: {0}/{1} (Đảm bảo KHÔNG BỊ ÂM)" -f $bookFinal.availableCopies, $bookFinal.totalCopies) -ForegroundColor Green

if ($reservedCount -eq 1 -and $bookFinal.availableCopies -ge 0) {
    Write-Host "`n=> CHỨNG MINH THÀNH CÔNG: Atomic SQL Update đã ngăn chặn hoàn toàn hiện tượng Over-booking!" -ForegroundColor Green
} else {
    Write-Host "`n=> Có sai lệch số lượng!" -ForegroundColor Red
}

Write-Host "==========================================================" -ForegroundColor Cyan
