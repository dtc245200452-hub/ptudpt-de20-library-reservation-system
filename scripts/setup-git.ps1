# Script tự động khởi tạo Git repository với lịch sử commit phân tầng rõ ràng
# Tên GitHub Repository khuyến nghị cho Đề tài 20: ptudpt-de20-library-reservation-system

param(
    [string]$GitHubUsername = ""
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   KHỞI TẠO GIT VÀ TẠO LỊCH SỬ COMMIT CHO ĐỀ TÀI 20" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Kiểm tra git có khả dụng không
try {
    $null = Get-Command git -ErrorAction Stop
} catch {
    Write-Host "Lỗi: Chưa tìm thấy 'git' trong PATH hệ thống!" -ForegroundColor Red
    Write-Host "Vui lòng cài đặt Git từ https://git-scm.com/ và mở lại terminal." -ForegroundColor Yellow
    exit 1
}

# 1. git init
Write-Host "`n[1/8] Khởi tạo Git repository..." -ForegroundColor Yellow
git init
git branch -M main

# 2. Commit 1: Khởi tạo dự án
Write-Host "`n[2/8] Commit 1: Khởi tạo cấu trúc dự án..." -ForegroundColor Yellow
git add .gitignore LibrarySystem.sln README.md .env.example HUONG-DAN-CHUNG.md 20-he-thong-thu-vien-dat-muon-sach.md
git commit -m "init: khởi tạo solution LibrarySystem và cấu hình dự án"

# 3. Commit 2: Contracts & Data Layer
Write-Host "`n[3/8] Commit 2: Shared Contracts và Data Layer..." -ForegroundColor Yellow
git add src/Library.Contracts/ src/Library.Data/
git commit -m "feat: Library.Contracts (DTOs, Enums, Events) và Library.Data (Entities, EF Core DbContext, SeedData)"

# 4. Commit 3: Web API
Write-Host "`n[4/8] Commit 3: Library API..." -ForegroundColor Yellow
git add src/Library.Api/
git commit -m "feat: Library.Api - ASP.NET Core Minimal API, Swagger, RabbitMQ Publisher, HealthChecks, CorrelationId"

# 5. Commit 4: Reservation Worker
Write-Host "`n[5/8] Commit 4: Reservation Worker..." -ForegroundColor Yellow
git add src/Library.Worker/
git commit -m "feat: Library.Worker - BackgroundService tiêu thụ RabbitMQ, Atomic SQL Update, Idempotency, DLQ"

# 6. Commit 5: Web Client
Write-Host "`n[6/8] Commit 5: Web Client..." -ForegroundColor Yellow
git add src/Library.WebClient/
git commit -m "feat: Library.WebClient - Razor Pages UI tra cứu sách, đặt mượn thời gian thực, quản lý giao nhận"

# 7. Commit 6: Dockerization
Write-Host "`n[7/8] Commit 6: Docker hóa..." -ForegroundColor Yellow
git add docker-compose.yml
git commit -m "feat: Docker hóa toàn bộ hệ thống với docker-compose.yml và các Dockerfile đa tầng"

# 8. Commit 7: Testing scripts
Write-Host "`n[8/8] Commit 7: Kịch bản kiểm thử..." -ForegroundColor Yellow
git add scripts/
git commit -m "test: bổ sung các kịch bản kiểm thử tự động (system test, resilience, race-condition)"

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "   HOÀN THÀNH TẠO 7 COMMITS CHUẨN MỰC TRÊN GIT!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

if ($GitHubUsername -ne "") {
    $remoteUrl = "https://github.com/$GitHubUsername/ptudpt-de20-library-reservation-system.git"
    git remote remove origin 2>$null
    git remote add origin $remoteUrl
    Write-Host "`nĐã gán remote origin: $remoteUrl" -ForegroundColor Cyan
    Write-Host "Chạy lệnh sau để push lên GitHub:" -ForegroundColor Yellow
    Write-Host "git push -u origin main" -ForegroundColor White
} else {
    Write-Host "`nĐể liên kết với GitHub, hãy tạo repository trên GitHub với tên:" -ForegroundColor Yellow
    Write-Host "ptudpt-de20-library-reservation-system" -ForegroundColor Green
    Write-Host "Sau đó chạy lệnh:" -ForegroundColor Yellow
    Write-Host "git remote add origin https://github.com/<tai-khoan-cua-ban>/ptudpt-de20-library-reservation-system.git" -ForegroundColor White
    Write-Host "git push -u origin main" -ForegroundColor White
}
