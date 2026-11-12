@echo off
chcp 65001 >nul
title Over-the-Horizon: C# EDITION (v0.2.0-alpha)
cd /d "%~dp0"

echo ============================================================================
echo   OVER THE HORIZON — C# EDITION (v0.2.0-alpha)
echo ============================================================================
echo Открывается главное меню с 3D-панорамой мира!
echo   - Singleplayer: одиночный мир с автосохранением построек
echo   - Multiplayer: подключение к другу ИЛИ запуск своего сервера в 1 клик!
echo   - E: палитра из 22 блоков   ^|   T: живой чат   ^|   F3: отладка   ^|   Esc: пауза
echo.

if exist "OverHorizon.Client.exe" (
    "OverHorizon.Client.exe" --view-distance 5
) else (
    dotnet run --project "../src/OverHorizon.Client/OverHorizon.Client.csproj" -c Release -- --view-distance 5
)
