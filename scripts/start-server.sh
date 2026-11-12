#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

echo "============================================================================"
echo "  Over-the-Horizon — Dedicated Multiplayer Server (TCP port 25565)"
echo "  Press Ctrl+C to flush world chunks safely to ./world and stop."
echo "============================================================================"

if [[ -x "./OverHorizon.Server" ]]; then
    exec ./OverHorizon.Server --port 25565 --view-distance 4 --world-dir ./world --op-all "$@"
else
    exec dotnet run --project ../src/OverHorizon.Server/OverHorizon.Server.csproj -c Release -- --port 25565 --view-distance 4 --world-dir ./world --op-all "$@"
fi
