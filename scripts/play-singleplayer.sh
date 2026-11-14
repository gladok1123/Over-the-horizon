#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

if [[ -x "./OverHorizon.Client" ]]; then
  ./OverHorizon.Client --singleplayer --view-distance 5
else
  dotnet run --project ../src/OverHorizon.Client/OverHorizon.Client.csproj -c Release -- --singleplayer --view-distance 5
fi
