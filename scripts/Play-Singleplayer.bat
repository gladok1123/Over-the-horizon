@echo off
chcp 65001 >nul
title Over-the-Horizon — Одиночная игра
cd /d "%~dp0"

echo ============================================================================
echo   Over-the-Horizon — Запуск одиночного мира (v0.1.0-alpha)
echo ============================================================================
echo Управление: WASD - ходьба, Shift/Ctrl - бег, Пробел - прыжок,
echo             ЛКМ - сломать блок, ПКМ - поставить блок, 1..9/колесо - выбор блока,
echo             F3 - статистика, Esc - освободить курсор мыши.
echo.

if exist "OverHorizon.Client.exe" (
    "OverHorizon.Client.exe" --view-distance 4
) else (
    dotnet run --project "../src/OverHorizon.Client/OverHorizon.Client.csproj" -c Release -- --view-distance 4
)
pause
