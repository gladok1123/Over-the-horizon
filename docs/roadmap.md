# Roadmap

## MVP `v0.1.0-alpha` — «Play With Friends» (завершён)

- [x] Core: математика, реестры, `BlockState` (interned), `ItemStack`, бинарный NBT
- [x] ECS на Arch + детерминированная параллельная интеграция (100-прогонные тесты хешей)
- [x] World: `PalettedContainer`, `ChunkSection`, `ChunkColumn` + CoW `ChunkSnapshot`, тикеты, асинхронное сохранение `.ohrg` регионов на диск (`--world-dir`)
- [x] Worldgen: Perlin/Simplex шум, биомы, пещеры, руды, деревья
- [x] Освещение async (sky + block BFS)
- [x] Physics: swept AABB с защитой от туннелирования, гравитация, жидкости, 3D DDA воксельный рейкаст (`VoxelRaycaster`)
- [x] Gameplay: игрок, 41-слотовый инвентарь, крафт 2×2/3×3, мобы + AI + 3D A* pathfinding
- [x] Networking + Server: TCP сервер (`--port`, `--view-distance`, `--seed`, `--world-dir`, `--op-all`), репликация чанков, блоков, чата и 3D-аватаров друзей (`0x06..0x08`)
- [x] Modding API: плагины (`AssemblyLoadContext`), `EventBus`, JSON datapacks, команды и права (`/help`, `/seed`, `/tp`, `/setblock`, `/op`)
- [x] Client + Rendering + Audio: Silk.NET OpenGL 3.3 Core, параллельный Greedy Chunk Meshing + AO, camera-relative рендеринг без дрожания на ±30M блоках, 3D-модели друзей с анимацией ходьбы, подсветка выбранного блока, 2D прицел и 9-слотовый строительный хотбар, позиционный 3D-звук OpenAL Soft
- [x] Автоматическая упаковка самодостаточных релизов (`win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`) со скриптами запуска в один клик

## Beta (следующий этап)

- [ ] Redstone полный (wires, repeaters, comparators, pistons, observers, dispensers, hoppers, rails)
- [ ] Печь, зельеварка, наковальня, стол зачарований
- [ ] Структуры (village, stronghold, fortress)
- [ ] Bedrock-протокол (второй кодек + тесты)
- [ ] Внутриигровой текстовый оверлей чата по клавише `T` и экран инвентаря `E`

## 1.0

- [ ] Полный survival-цикл, advancements, статистика
- [ ] Кастомные PNG-скины игроков, расширенные анимации
- [ ] Каскадные тени (CSM) и отражения воды
- [ ] Полная профилировочная оптимизация под 100+ одновременных игроков на одном процессе

## Вне scope MVP

- Mixin-совместимость (не реализуется; только явные типизированные extension-точки).
- 1:1 бинарная совместимость с конкретными проприетарными клиентами Mojang.
- Проприетарные текстуры/звуки Mojang (используются процедурно синтезируемые clean-room ассеты).
