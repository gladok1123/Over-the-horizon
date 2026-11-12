# Over-the-horizon

Educational/technical reimplementation of a Minecraft-like voxel engine in C# (.NET 9) with Horizon-style multithreading. **Не аффилирован с Mojang/Microsoft. Не использует их код и ассеты.**

## Статус

- Шаг 1 (анализ и план) — завершён. См. `docs/`.
- Шаг 2 (скелет решения, DI, логирование, анализаторы, CI, архитектурные тесты) — завершён. См. `OverHorizon.sln`.
- Шаг 3 (Core: математика, реестры, интернированный `BlockState`, `ItemStack`, бинарный NBT, детерминированный ГСЧ/хеширование и параллельный Arch ECS с 100-кратным тестом детерминизма) — завершён.
- Шаг 4 (World: `BitStorage`, `PalettedContainer<T>`, `ChunkSection`, `ChunkColumn` + CoW `ChunkSnapshot`, `PerlinNoise`/`SimplexNoise2D` worldgen, пещеры, биомы, деревья, `LightingEngine` sky+block, `ChunkNbtSerializer`, `FileChunkStorage` на `System.IO.Pipelines` и `ChunkManager` с тикетами и `Channel<T>`) — завершён.
- Шаг 5 (Physics + Gameplay: swept AABB с защитой от туннелирования, гравитация/плавучесть/течение и клеточная симуляция жидкостей, 41-слотовый инвентарь, крафт 2×2/3×3 shaped+shapeless, 3D воксельный A* pathfinding, правила спавна мобов, урон/здоровье и 100-кратный детерминированный тик World + Physics + Arch ECS) — завершён.
- Шаг 6 (Networking + Server MVP: `VarInt`/`VarLong`, `PacketFramer` на `System.IO.Pipelines` с ZLib-сжатием и защитой от zip-bomb, `JavaProtocolCodec`, source-generated JSON статуса, асинхронный `NetworkServer` + `JavaProtocolClient`, `ChunkNetworkSerializer`, `ServerPlaySessionBridge` с авторитативным античитом движения, мультиплеерной синхронизацией блоков и чата, fuzz-тестами и headless TCP loopback интеграционными тестами) — завершён.
- Шаг 7 (Modding API + Commands & Permissions: `ModManifest`, топологический резолвер зависимостей `ModDependencySorter`, выгружаемый `PluginAssemblyLoader` на `AssemblyLoadContext`, AOT JSON `DataPackLoader`, без-аллокационный `EventBus` с приоритетами и отменой событий, `CommandDispatcher` с `PermissionLevel` и встроенными командами `/help`, `/seed`, `/tp`, `/setblock`, `/op`) — завершён.
- Шаг 8 (Client + Rendering + Audio на Silk.NET: процедурный RGBA8-атлас без проприетарных ассетов, параллельный Greedy Chunk Meshing с межчанковым face culling и плавным вершинным Ambient Occlusion, camera-relative рендерер Silk.NET OpenGL 3.3 + `HeadlessRenderSubsystem`, Gribb-Hartmann frustum culling, двухпроходная отрисовка непрозрачной геометрии и полупрозрачной воды с линейным туманом, F3 телеметрия, позиционное 3D-аудио Silk.NET OpenAL с процедурным 16-bit PCM синтезатором звуков и клиентская сессия singleplayer/multiplayer TCP) — завершён.
- Решения: кастомный MVP, сервер-first, Silk.NET, Arch ECS, протокол Java + Bedrock (фазово).

## Сборка и тесты

```bash
dotnet restore OverHorizon.sln
dotnet build OverHorizon.sln --configuration Release
dotnet test tests/OverHorizon.Tests/OverHorizon.Tests.csproj --configuration Release
```

## Документация

- `docs/architecture.md` — целевая архитектура
- `docs/threading.md` — модель потоков и фазы тика
- `docs/roadmap.md` — MVP → Beta → 1.0
- `docs/protocol.md` — стратегия протокола
- `docs/modding.md` — Modding API
- `docs/benchmarks.md` — цели производительности
- `docs/adr/` — Architecture Decision Records

## Лицензия

Проект — Apache-2.0 (планируется). Без копирования кода Mojang/Horizon и без проприетарных ассетов.
