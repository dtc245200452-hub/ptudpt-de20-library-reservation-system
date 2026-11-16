@echo off
chcp 65001 >nul
echo ==========================================================
echo    DANG TIEN HANH PUSH CODE LEN GITHUB REPOSITORY:
echo    https://github.com/dtc245200452-hub/ptudpt-de20-library-reservation-system
echo ==========================================================
set "PATH=%LOCALAPPDATA%\Programs\Git\cmd;%PATH%"
cd /d "%~dp0"

echo.
echo Dang kiem tra remote origin...
git remote -v

echo.
echo Neu day la lan dau dang nhap, cua so trinh duyet hoac GitHub
echo se hien len de ban xac nhan tai khoan 'dtc245200452-hub'.
echo.
git push -u origin main

if %ERRORLEVEL% equ 0 (
    echo.
    echo ==========================================================
    echo    CHUC MUNG! DA PUSH TOAN BO CODE LEN GITHUB THANH CONG!
    echo ==========================================================
) else (
    echo.
    echo ==========================================================
    echo    Luu y: Neu gap loi xac thuc, ban co the:
    echo    1. Tao Personal Access Token (PAT) tren GitHub (Settings -> Developer Settings -> Personal access tokens)
    echo    2. Hoac nhap Token vao khi duoc hoi password.
    echo ==========================================================
)
echo.
pause
