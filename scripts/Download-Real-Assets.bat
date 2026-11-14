@echo off
chcp 65001 >nul
title Over-the-Horizon — Загрузка ассетов 23w13a__or__b в ./resourcepacks
cd /d "%~dp0"

echo ============================================================================
echo   Загрузка текстур и звуков из InventivetalentDev/minecraft-assets (23w13a__or__b)
echo ============================================================================
echo Примечание: OverHorizon.Client.exe также автоматически кэширует нужные PNG и OGG
echo из ветки 23w13a__or__b при первом запуске! Этот скрипт скачивает полный архив ветки.
echo.

if not exist "resourcepacks" mkdir "resourcepacks"
curl.exe -L "https://codeload.github.com/InventivetalentDev/minecraft-assets/zip/refs/heads/23w13a__or__b" -o "resourcepacks\minecraft-assets-23w13a__or__b.zip"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [ГОТОВО!] Архив сохранён в resourcepacks\minecraft-assets-23w13a__or__b.zip
    echo Запускайте Play-OverHorizon.bat или OverHorizon.Client.exe!
) else (
    echo.
    echo [ИНФО] Попытка быстрой синхронизации через Download-Real-Assets.ps1 ...
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Download-Real-Assets.ps1"
)
pause
