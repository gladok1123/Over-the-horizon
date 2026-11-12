@echo off
chcp 65001 >nul
title Over-the-Horizon — Мультиплеерный Сервер (v0.1.0-alpha)
cd /d "%~dp0"

echo ============================================================================
echo   Over-the-Horizon — Запуск сервера для игры с друзьями (порт TCP 25565)
echo ============================================================================
echo   Подсказка для друзей:
echo   1. В одной Wi-Fi / LAN сети или через Radmin VPN / Tailscale / ZeroTier:
echo      передайте друзьям ваш IPv4 адрес ниже с портом :25565
echo   2. Без VPN: запустите playit.gg (TCP Tunnel -> 127.0.0.1:25565)
echo ----------------------------------------------------------------------------
ipconfig | findstr /i "IPv4"
echo ============================================================================
echo   Для безопасного сохранения мира нажмите Ctrl+C в этом окне.
echo.

if exist "OverHorizon.Server.exe" (
    "OverHorizon.Server.exe" --port 25565 --view-distance 4 --world-dir "./world" --op-all
) else (
    dotnet run --project "../src/OverHorizon.Server/OverHorizon.Server.csproj" -c Release -- --port 25565 --view-distance 4 --world-dir "./world" --op-all
)
pause
