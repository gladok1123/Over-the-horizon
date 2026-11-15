#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

if [[ -x "./OverHorizon.Client" ]]; then
  exec ./OverHorizon.Client --view-distance 6 "$@"
elif [[ -x "../OverHorizon.Client" ]]; then
  cd ..
  exec ./OverHorizon.Client --view-distance 6 "$@"
else
  exec dotnet run --project ../src/OverHorizon.Client/OverHorizon.Client.csproj -c Release -- --view-distance 6 "$@"
fi
