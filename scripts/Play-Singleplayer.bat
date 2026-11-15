@echo off
chcp 65001 >nul
title Over-the-Horizon: C# EDITION — Быстрый старт (Одиночный мир)
cd /d "%~dp0"

if exist "OverHorizon.Client.exe" (
    "OverHorizon.Client.exe" --singleplayer --view-distance 5
) else (
    dotnet run --project "../src/OverHorizon.Client/OverHorizon.Client.csproj" -c Release -- --singleplayer --view-distance 5
)
