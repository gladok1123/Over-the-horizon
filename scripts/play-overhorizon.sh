#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

echo "============================================================================"
echo "  OVER THE HORIZON — C# EDITION (v0.2.0-alpha)"
echo "============================================================================"
echo "Открывается главное меню с 3D-панорамой мира!"
echo "  - Singleplayer: одиночный мир с автосохранением"
echo "  - Multiplayer: подключение к другу ИЛИ запуск своего сервера в 1 клик!"
echo "  - E: палитра 22 блоков | T: чат | F3: отладка | Esc: меню паузы"
echo

if [[ -x "./OverHorizon.Client" ]]; then
  ./OverHorizon.Client --view-distance 5
else
  dotnet run --project ../src/OverHorizon.Client/OverHorizon.Client.csproj -c Release -- --view-distance 5
fi
