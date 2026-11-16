@echo off
chcp 65001 >nul
echo ==========================================================
echo    KHOI TAO GIT CHO DE TAI 20 - THU VIEN PHAN TAN
echo ==========================================================
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\setup-git.ps1" %*
pause
