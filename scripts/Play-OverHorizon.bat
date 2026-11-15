@echo off
chcp 65001 >nul
title Over-the-Horizon: C# EDITION (v0.6.0-alpha)
cd /d "%~dp0"

if exist "OverHorizon.Client.exe" (
    start "" "OverHorizon.Client.exe" --view-distance 6
) else if exist "..\OverHorizon.Client.exe" (
    cd /d "%~dp0.."
    start "" "OverHorizon.Client.exe" --view-distance 6
) else (
    dotnet run --project "../src/OverHorizon.Client/OverHorizon.Client.csproj" -c Release -- --view-distance 6
)
