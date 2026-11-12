# Архитектура Over-the-horizon

> Educational/technical reimplementation. Не аффилирован с Mojang/Microsoft.
> Не использует код и ассеты Minecraft. Реализация по документации minecraft.wiki.

## 1. Принципы

- Clean Architecture: `Domain -> Application -> Infrastructure -> Presentation`.
- SOLID, DRY, KISS; производительность — в горячих путях.
- Нет глобальных статических синглтонов (кроме immutable-констант). Всё через DI.
- Immutable-конфигурации (`record`), mutable-состояние — только где оправдано.
- Нет циклических зависимостей между проектами (проверка: `dotnet list reference` + анализаторы).
- Детерминизм параллельных фаз: барьеры + детерминированный merge по ID.
- AOT-готовность сервера: без рефлексии в hot-path, source generators вместо runtime-reflection.

## 2. Решения (зафиксированы пользователем)

| Тема | Решение | ADR |
|---|---|---|
| Целевая версия | Кастомный MVP без жёсткой привязки к версии MC | [0003](adr/0003-target-version.md) |
| Приоритет | Сервер в первую очередь (dedicated + headless) | [0005](adr/0005-server-first.md) |
| Графика | Silk.NET (OpenGL 4.6 + Vulkan) | [0002](adr/0002-graphics-api.md) |
| ECS | Arch | [0001](adr/0001-ecs-choice.md) |
| Протокол | Java + Bedrock (фазово: сначала Java-ядро) | [0004](adr/0004-protocol.md) |
| BlockState | Interned `ushort` ID + flyweight | [0006](adr/0006-blockstate-interning.md) |

## 3. Структура решения

```
Solution/
├── src/
│   ├── OverHorizon.Core/          # Domain: блоки/сущности/предметы, математика, реестры, NBT
│   ├── OverHorizon.World/         # Чанки, worldgen, освещение, хранение
│   ├── OverHorizon.Physics/       # AABB, коллизии, жидкости
│   ├── OverHorizon.Gameplay/        # Игрок, инвентарь, крафт, redstone, AI
│   ├── OverHorizon.Rendering/     # Silk.NET, мешинг чанков, шейдеры (клиент, позже)
│   ├── OverHorizon.Networking/    # Протокол Java + Bedrock, пакеты, сессии
│   ├── OverHorizon.Audio/         # OpenAL (позже)
│   ├── OverHorizon.Modding/       # Плагины, event bus, datapack
│   ├── OverHorizon.Shared/        # Общие утилиты, коллекции, RNG
│   ├── OverHorizon.Server/        # Dedicated server entry point (приоритет)
│   └── OverHorizon.Client/        # Client entry point (после MVP сервера)
├── tests/
│   ├── OverHorizon.Tests/         # xUnit + FsCheck
│   └── OverHorizon.Benchmarks/    # BenchmarkDotNet
└── docs/
```

Зависимости (без циклов):

- `Core` — ни от кого не зависит (чистый Domain без внешних пакетов).
- `Shared` — зависит только от `Core` (зафиксировано на Шаге 2).
- `World`, `Physics` — зависят от `Core`, `Shared`.
- `Gameplay` — зависит от `Core`, `Shared`, `World`, `Physics` (+ пакет `Arch`).
- `Networking`, `Modding` — зависят от `Core`, `Shared`.
- `Rendering` — зависит от `Core`, `Shared`, `World` (клиент).
- `Audio` — зависит от `Core`, `Shared` (клиент).
- `Server` — зависит от `Core`, `Shared`, `World`, `Physics`, `Gameplay`, `Networking`, `Modding`.
- `Client` — зависит от `Core`, `Shared`, `World`, `Physics`, `Gameplay`, `Rendering`, `Audio`, `Networking`, `Modding`.

## 4. Ключевые сервисы (вместо God-классов)

| God-класс MC | Замена |
|---|---|
| `MinecraftServer` | `IWorldTicker`, `IChunkManager`, `IPlayerManager`, `INetworkServer` |
| `ServerWorld` | `IWorld` + `IWorldTicker` + `IChunkManager` + `ILightingSystem` |
| `ClientWorld` | `IClientWorld` (клиент, позже) |
| `Chunk` | `IChunk` (immutable-снимок) + mutable-дельты (CoW) |
| `Block` | `IBlockRegistry` + `BlockState` (interned) |

## 5. Core: реестры и состояния

- Типизированные реестры (`IBlockRegistry`, `IItemRegistry`, `IEntityRegistry`, `IBiomeRegistry`) с compile-time регистрацией через source generators.
- `BlockState` — `readonly struct` с `ushort Id`, structural sharing свойств (см. ADR 0006).
- `ItemStack` — `readonly record struct`, immutable + copy-on-write.
- `NbtTag` — NBT-совместимый reader/writer (бинарный) + JSON для datapack.
- Математика: `double` для мировых координат, `float` для рендера/физики; `BlockPos`/`ChunkPos` — `readonly struct` (int).

## 6. World

- Чанк 16×16×384, `PalettedContainer<T>` с бит-упаковкой, секции 16³.
- `Chunk` — immutable-снимок + CoW-дельты, double buffering для параллельного доступа.
- Chunk tickets (как в MC), priority queue по расстоянию до игрока.
- Async load/save через `Channel<T>` + `ValueTask`, `System.IO.Pipelines`.
- Worldgen: OpenSimplex2 + Perlin (своя реализация), density functions, biome source, carvers, features; JSON datapack-форматы (близкие к MC, без гарантии 1:1 из-за кастомного MVP).
- Освещение: sky + block light, async propagation (см. `threading.md`).

## 7. Gameplay + ECS (Arch)

- Компоненты: `Position`, `Velocity`, `Health`, `AI`, `Render`, `Inventory` и т.д.
- Параллельный AI-тик по read-only snapshot; детерминированный merge мутаций.
- Pathfinding A*, spawn rules (свет, биом, время).
- Инвентарь 36+броня+offhand, крафт 2×2/3×3, печь, зельеварка, наковальня, стол зачарований (фазами по roadmap).
- Redstone: async propagation с приоритетными очередями (после MVP).

## 8. Networking

- Два кодека: Java-подобный (VarInt, handshake/login/play) и Bedrock-подобный — см. `protocol.md`.
- `System.IO.Pipelines`, source-generated сериализация, compression (zlib/gzip), backpressure.
- Фаза 1: Java-ядро + кастомный внутренний протокол. Фаза 2: Bedrock.

## 9. Modding

- Плагины через интерфейсы + assembly load, event bus (`IEventBus`) с source generators.
- Data-driven контент: JSON, близкий к MC datapack (без Mixin).
- См. `modding.md`.

## 10. Качество

- Nullable reference types, ImplicitUsings, file-scoped namespaces.
- `TreatWarningsAsErrors=true`; анализаторы: NetAnalyzers, Roslynator, StyleCop, Meziantou, Threading.Analyzers.
- Тесты: xUnit + FluentAssertions + FsCheck; fuzz для пакетов/NBT; тесты детерминизма (хеш мира).
- Покрытие: ≥70% Domain, ≥50% в целом (цель, не блокер MVP).
