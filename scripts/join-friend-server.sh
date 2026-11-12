#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

read -r -p "Server address [127.0.0.1:25565]: " SERVER_ADDR
SERVER_ADDR="${SERVER_ADDR:-127.0.0.1:25565}"

read -r -p "Your username (max 16 chars) [Explorer]: " PLAYER_NAME
PLAYER_NAME="${PLAYER_NAME:-Explorer}"

echo "Connecting to ${SERVER_ADDR} as '${PLAYER_NAME}'..."

if [[ -x "./OverHorizon.Client" ]]; then
    exec ./OverHorizon.Client --connect "${SERVER_ADDR}" --username "${PLAYER_NAME}" --view-distance 4 "$@"
else
    exec dotnet run --project ../src/OverHorizon.Client/OverHorizon.Client.csproj -c Release -- --connect "${SERVER_ADDR}" --username "${PLAYER_NAME}" --view-distance 4 "$@"
fi
