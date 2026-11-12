# Over-the-Horizon (`v0.1.0-alpha` — Play With Friends)

Образовательная и инженерная clean-room реимплементация воксельного мультиплеерного движка в духе Minecraft Java Edition на **C# 13 / .NET 9** с многопоточной архитектурой в стиле **Horizon** (детерминированные фазы тика, Copy-on-Write снапшоты чанков, параллельный Greedy Meshing + Ambient Occlusion, Arch ECS, `System.IO.Pipelines` и `Channel<T>`).

> **Дисклеймер:** Проект создан исключительно в образовательных и исследовательских целях. **Не аффилирован и не одобрен Mojang Studios или Microsoft.** Не содержит ни строчки кода Mojang/Horizon и не использует проприетарные текстуры или звуки (все текстуры и 3D-звуки синтезируются процедурно на лету).

---

## Быстрый старт: Как поиграть с друзьями прямо сейчас

Готовые самодостаточные сборки (**установка `.NET` не требуется!**) автоматически собираются для **Windows (`win-x64`)**, **Linux (`linux-x64`)** и **macOS (`osx-arm64`, `osx-x64`)** в разделе **[GitHub Releases — `v0.1.0-alpha`](https://github.com/gladok1123/Over-the-horizon/releases/tag/v0.1.0-alpha)**.

1. **Скачайте и распакуйте** архив под вашу ОС (например, `OverHorizon-v0.1.0-alpha-win-x64.zip`).
2. **Хост (кто держит мир):** запустите `Start-Server.bat` (или `./start-server.sh`). Мир автоматически сохраняется в папку `./world`.
3. **Подключение к игре (вы и ваши друзья):**
   - Запустите `Join-Friend-Server.bat` (или `./join-friend-server.sh`).
   - Хост вводит `127.0.0.1:25565`, а друзья вводят ваш IP в **Radmin VPN / Tailscale** (`26.x.x.x:25565`) или публичный TCP-туннель **playit.gg** и свой никнейм!
4. **Одиночная игра:** запустите `Play-Singleplayer.bat` (или `./play-singleplayer.sh`).

Подробное руководство по сетевой игре и управлению: **[`docs/play-with-friends.md`](docs/play-with-friends.md)**.

### Управление в игре
- **`W / A / S / D`** — ходьба, **`Shift` / `Ctrl`** — бег, **`Пробел`** — прыжок, **Мышь** — камера.
- **ЛКМ** — сломать подсвеченный блок (до 6 блоков), **ПКМ** — поставить выбранный блок.
- **`1`..`9` или колесо мыши** — выбор блока в хотбаре (`булыжник`, `камень`, `земля`, `дёрн`, `доски`, `бревно`, `песок`, `листва`, `светящийся камень`).
- **`F3`** — FPS, координаты и число друзей онлайн в заголовке окна; **`Esc`** — освободить курсор мыши.

---

## Завершённые итерации разработки (Шаги 1–9)

- **Шаг 1 (Архитектура и ADR)** — чистая модульная DAG-архитектура из 11 проектов без циклических зависимостей, модель потоков и фаз тика (`docs/`).
- **Шаг 2 (Решение, DI, анализаторы, CI)** — `OverHorizon.sln`, `TreatWarningsAsErrors=true`, 5 пакетов статических анализаторов, архитектурные тесты (`<= 500` строк на файл).
- **Шаг 3 (`Core` + `Shared` + Arch ECS)** — математика, замороженные реестры, интернированный `BlockState`, `ItemStack`, бинарный NBT, детерминированный `Xoroshiro128PlusPlus` и параллельный планировщик систем ECS.
- **Шаг 4 (`World`)** — `BitStorage`, `PalettedContainer<T>`, `ChunkColumn` + иммутабельный `ChunkSnapshot`, шумы Перлина/Симплекса, биомы, пещеры, деревья, BFS-освещение (sky + block), `.ohrg` регионы и `ChunkManager`.
- **Шаг 5 (`Physics` + `Gameplay`)** — swept-AABB коллизии без туннелирования, жидкости, 41-слотовый инвентарь, крафт 2×2/3×3, мобы и 3D A* pathfinding.
- **Шаг 6 (`Networking` + `Server`)** — `VarInt`, `PacketFramer` на `System.IO.Pipelines` с ZLib-сжатием, `JavaProtocolCodec`, серверный античит движения и репликация мира по TCP.
- **Шаг 7 (`Modding` + Команды)** — выгружаемые плагины (`AssemblyLoadContext`), `EventBus`, JSON datapacks, диспетчер команд (`/help`, `/seed`, `/tp`, `/setblock`, `/op`).
- **Шаг 8 (`Rendering` + `Audio` + `Client`)** — процедурный текстурный атлас, параллельный Greedy Chunk Meshing + плавный вершина-AO, camera-relative рендерер Silk.NET OpenGL 3.3 Core, Frustum Culling, позиционный 3D-звук OpenAL Soft.
- **Шаг 9 (`v0.1.0-alpha` — Playable Multiplayer Build)** — 3D DDA рейкастер блоков (`VoxelRaycaster`), интерактивное разрушение и строительство, 9-слотовый хотбар и прицел, репликация и 3D-рендеринг анимированных аватаров друзей (`RemotePlayerSpawn`/`Move`/`Despawn`), автосохранение мира на диск (`--world-dir`), скрипты запуска в один клик и самодостаточные релизы в GitHub Releases.

---

## Сборка и запуск тестов из исходников

Требуется **.NET 9 SDK (`9.0.100+`)**:

```bash
dotnet restore OverHorizon.sln
dotnet build OverHorizon.sln --configuration Release -warnaserror
dotnet test tests/OverHorizon.Tests/OverHorizon.Tests.csproj --configuration Release
```

Запуск микробенчмарков `BenchmarkDotNet`:

```bash
dotnet run --project tests/OverHorizon.Benchmarks/OverHorizon.Benchmarks.csproj -c Release -- --filter "*"
```

## Документация

- [`docs/play-with-friends.md`](docs/play-with-friends.md) — инструкция по запуску сервера и совместной игре с друзьями
- [`docs/architecture.md`](docs/architecture.md) — модульная архитектура и границы слоёв
- [`docs/threading.md`](docs/threading.md) — модель потоков, владение данными и фазы тика
- [`docs/protocol.md`](docs/protocol.md) — таблица сетевых пакетов и стратегия протокола
- [`docs/modding.md`](docs/modding.md) — Modding API, события и команды
- [`docs/benchmarks.md`](docs/benchmarks.md) — цели производительности и бенчмарки
- [`docs/roadmap.md`](docs/roadmap.md) — план развития `MVP → Beta → 1.0`
- [`docs/adr/`](docs/adr/) — Architecture Decision Records (`ADR-0001` .. `ADR-0006`)

## Лицензия

Распространяется под лицензией **Apache-2.0**.
