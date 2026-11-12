@echo off
chcp 65001 >nul
title Over-the-Horizon — Загрузка пакета текстур в ./resourcepacks
cd /d "%~dp0"

echo ============================================================================
echo   Загрузка текстур из InventivetalentDev/minecraft-assets (ветка 1.21.4)
echo ============================================================================
echo Все права на оригинальные ресурсы Minecraft принадлежат Mojang Studios.
echo Скрипт скачивает архив локально на ваш ПК в папку .\resourcepacks\ —
echo движок Over-the-Horizon автоматически подхватит PNG-текстуры при запуске!
echo.

if not exist "resourcepacks" mkdir "resourcepacks"
curl.exe -L "https://codeload.github.com/InventivetalentDev/minecraft-assets/zip/refs/heads/1.21.4" -o "resourcepacks\minecraft-assets-1.21.4.zip"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [ГОТОВО!] Архив сохранён в resourcepacks\minecraft-assets-1.21.4.zip
    echo Теперь запускайте Play-OverHorizon.bat или OverHorizon.Client.exe!
) else (
    echo.
    echo [ОШИБКА] Не удалось скачать архив. Вы также можете положить любой .zip
    echo текстур-пак или .jar файл вручную в папку .\resourcepacks\ !
)
pause
