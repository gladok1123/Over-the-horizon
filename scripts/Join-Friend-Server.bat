@echo off
chcp 65001 >nul
title Over-the-Horizon — Подключение к серверу друга (v0.6.0-alpha)
cd /d "%~dp0"

set /p SERVER_ADDR="Введите адрес сервера [127.0.0.1:25565]: "
if "%SERVER_ADDR%"=="" set SERVER_ADDR=127.0.0.1:25565

set /p PLAYER_NAME="Введите ваш никнейм (до 16 символов, латиница) [Explorer]: "
if "%PLAYER_NAME%"=="" set PLAYER_NAME=Explorer

if exist "..\OverHorizon.Client.exe" (
    cd /d "%~dp0.."
    "OverHorizon.Client.exe" --connect "%SERVER_ADDR%" --username "%PLAYER_NAME%" --view-distance 5
) else if exist "OverHorizon.Client.exe" (
    "OverHorizon.Client.exe" --connect "%SERVER_ADDR%" --username "%PLAYER_NAME%" --view-distance 5
) else (
    dotnet run --project "../src/OverHorizon.Client/OverHorizon.Client.csproj" -c Release -- --connect "%SERVER_ADDR%" --username "%PLAYER_NAME%" --view-distance 5
)
pause
