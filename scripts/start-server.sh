#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

echo "============================================================================"
echo "  Over-the-Horizon v0.6.0-alpha — Dedicated Multiplayer Server (TCP 25565)"
echo "  Press Ctrl+C to flush world chunks safely and stop."
echo "============================================================================"

if [[ -x "./OverHorizon.Server" ]]; then
    exec ./OverHorizon.Server --port 25565 --view-distance 4 --world-dir ../saves/multiplayer_world --op-all "$@"
elif [[ -x "./server/OverHorizon.Server" ]]; then
    exec ./server/OverHorizon.Server --port 25565 --view-distance 4 --world-dir ./saves/multiplayer_world --op-all "$@"
else
    exec dotnet run --project ../src/OverHorizon.Server/OverHorizon.Server.csproj -c Release -- --port 25565 --view-distance 4 --world-dir ./world --op-all "$@"
fi
