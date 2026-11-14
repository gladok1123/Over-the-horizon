@echo off
chcp 65001 >nul
title Over-the-Horizon — Выделенный Сервер (v0.6.0-alpha, TCP 25565)
cd /d "%~dp0"

echo ============================================================================
echo   Over-the-Horizon v0.6.0-alpha — Сервер для игры с друзьями (порт TCP 25565)
echo ============================================================================
ipconfig | findstr /i "IPv4"
echo ============================================================================
echo   Для безопасного сохранения мира нажмите Ctrl+C в этом окне.
echo.

if exist "OverHorizon.Server.exe" (
    "OverHorizon.Server.exe" --port 25565 --view-distance 4 --world-dir "../saves/multiplayer_world" --op-all
) else if exist "server\OverHorizon.Server.exe" (
    "server\OverHorizon.Server.exe" --port 25565 --view-distance 4 --world-dir "./saves/multiplayer_world" --op-all
) else (
    dotnet run --project "../src/OverHorizon.Server/OverHorizon.Server.csproj" -c Release -- --port 25565 --view-distance 4 --world-dir "./world" --op-all
)
pause
