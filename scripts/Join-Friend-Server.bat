@echo off
chcp 65001 >nul
title Over-the-Horizon — Подключение к серверу друга
cd /d "%~dp0"

echo ============================================================================
echo   Over-the-Horizon — Вход на сервер к друзьям (v0.1.0-alpha)
echo ============================================================================
set /p SERVER_ADDR="Введите адрес сервера [по умолчанию 127.0.0.1:25565]: "
if "%SERVER_ADDR%"=="" set SERVER_ADDR=127.0.0.1:25565

set /p PLAYER_NAME="Введите ваш никнейм (до 16 символов, латиница) [Explorer]: "
if "%PLAYER_NAME%"=="" set PLAYER_NAME=Explorer

echo.
echo Подключаемся к %SERVER_ADDR% под ником %PLAYER_NAME%...
echo Управление: WASD - ходьба, Shift/Ctrl - бег, Пробел - прыжок,
echo             ЛКМ - сломать блок, ПКМ - поставить блок, 1..9/колесо - выбор блока,
echo             F3 - статистика в заголовке, Esc - освободить курсор мыши.
echo.

if exist "OverHorizon.Client.exe" (
    "OverHorizon.Client.exe" --connect "%SERVER_ADDR%" --username "%PLAYER_NAME%" --view-distance 4
) else (
    dotnet run --project "../src/OverHorizon.Client/OverHorizon.Client.csproj" -c Release -- --connect "%SERVER_ADDR%" --username "%PLAYER_NAME%" --view-distance 4
)
pause
