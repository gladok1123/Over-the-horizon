# Roadmap

## MVP (сервер-first, кастомный)

- [x] Core: математика, реестры, BlockState (interned), ItemStack, NBT
- [x] ECS на Arch + интеграция
- [x] World: PalettedContainer, ChunkSection, Chunk, chunk manager, tickets, async I/O
- [x] Worldgen: noise, density, biomes, carvers, features (JSON datapack-близкие)
- [x] Освещение async (sky + block)
- [x] Physics: AABB, gravity, fluids (база)
- [x] Gameplay: игрок, инвентарь, крафт 2×2/3×3, базовые мобы + AI + pathfinding
- [x] Networking: Java-ядро (handshake/login/play) + кастомный внутренний протокол
- [x] Headless-тесты + тесты детерминизма
- [x] Критерий: single-player creative + базовый survival на сервере

## Beta

- [ ] Redstone полный (wires, repeaters, comparators, pistons, observers, dispensers, hoppers, rails)
- [ ] Печь, зельеварка, наковальня, стол зачарований
- [ ] Структуры (village, stronghold, fortress)
- [ ] Bedrock-протокол (второй кодек + тесты)
- [x] Modding API: плагины, event bus, datapack-совместимость
- [x] Клиент: окно (Silk.NET), chunk meshing parallel, текстуры, UI, F3 overlay
- [x] Аудио: OpenAL, позиционный звук

## 1.0

- [ ] Полный survival-цикл, advancements, статистика
- [x] Команды + permission system
- [ ] Скины, модель игрока, анимации
- [x] Вода, туман; greedy meshing + AO (базовые шейдеры GLSL 330); тени — в плане 1.0
- [ ] Benchmarks + профилирование + оптимизация
- [ ] Документация полная, релиз

## Вне scope MVP

- Mixin-совместимость (не реализуется; только явные extension-точки).
- 1:1 совместимость с конкретными версиями MC (кастомный MVP).
- Проприетарные ассеты Mojang (только свободные/генерируемые).
