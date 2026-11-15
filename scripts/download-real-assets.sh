#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

echo "============================================================================"
echo "  Загрузка текстур из InventivetalentDev/minecraft-assets (ветка 1.21.4)"
echo "============================================================================"
echo "Все права на оригинальные ресурсы Minecraft принадлежат Mojang Studios."
echo "Архив скачивается локально в ./resourcepacks/ и автоматически подхватывается игрой."
echo

mkdir -p resourcepacks
curl -L "https://codeload.github.com/InventivetalentDev/minecraft-assets/zip/refs/heads/1.21.4" \
  -o "resourcepacks/minecraft-assets-1.21.4.zip"

echo
echo "[ГОТОВО!] Запускайте ./play-overhorizon.sh или ./OverHorizon.Client!"
