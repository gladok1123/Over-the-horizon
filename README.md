# Over-the-Horizon: C# EDITION (`v0.3.0-alpha`)

Образовательная и инженерная 100% clean-room воксельная песочница на **C# 13 / .NET 9** с главным меню **«OVER THE HORIZON — C# EDITION»**, вращающейся 3D-панорамой мира, палитрой из 22 блоков (`E`), живым чатом (`T`) и многопоточной архитектурой в стиле **Horizon** (детерминированные фазы тика, Copy-on-Write снапшоты чанков, параллельный Greedy Meshing + Ambient Occlusion, Arch ECS, `System.IO.Pipelines` и `Channel<T>`).

> **Дисклеймер:** Проект создан с нуля (clean-room, Apache-2.0) в образовательных и инженерных целях. **Не аффилирован и не одобрен Mojang Studios или Microsoft.** Не содержит ни строчки кода Mojang и не использует проприетарные текстуры, шрифты или звуки (все 16×16 пиксель-арт текстуры, растровый 8×8 шрифт и 3D-звуки синтезируются процедурно в чистом C#).

---

## Быстрый старт: Как запустить игру и поиграть с друзьями

Готовые самодостаточные сборки (**установка `.NET` не требуется!**) автоматически собираются для **Windows (`win-x64`)**, **Linux (`linux-x64`)** и **macOS (`osx-arm64`, `osx-x64`)** в разделе **[GitHub Releases — `v0.3.0-alpha`](https://github.com/gladok1123/Over-the-horizon/releases/tag/v0.3.0-alpha)**.

1. **Скачайте и распакуйте** архив под вашу ОС (например, `OverHorizon-v0.3.0-alpha-win-x64.zip`).
2. *(По желанию)* Запустите **`Download-Real-Assets.bat`** (или `./download-real-assets.sh`), либо положите любой ресурс-пак `.zip` / `.jar` в папку `./resourcepacks/` — встроенный парсер `ResourcePackLoader` автоматически загрузит PNG-текстуры блоков и `.wav` звуки!
2. **Запустите `Play-OverHorizon.bat`** (или `./play-overhorizon.sh`, либо просто дважды кликните `OverHorizon.Client.exe`!).
3. Откроется главное меню **OVER THE HORIZON — C# EDITION** с вращающейся 3D-панорамой мира:
   - **`Singleplayer`** — одиночный мир с автоматическим сохранением построек в `./saves/SingleplayerWorld`.
   - **`Multiplayer (Play With Friends)`** — нажмите **`Host Local Server (25565) & Play`**, чтобы в **1 клик** поднять сервер и сразу зайти в мир! Ваши друзья вводят ваш IP в **Radmin VPN / Tailscale** (`26.x.x.x:25565`) и свой ник и нажимают **`Join Friend's Server`**.
   - **`Options...`** — настройка `FOV`, `Render Distance`, громкости звука и чувствительности мыши прямо в окне.

Подробное руководство по сетевой игре и управлению: **[`docs/play-with-friends.md`](docs/play-with-friends.md)**.

### Управление в игре
- **`W / A / S / D`** — ходьба, **`Shift` / `Ctrl`** — бег, **`Пробел`** — прыжок, **Мышь** — камера.
- **ЛКМ** — сломать блок (с 3D-анимацией взмаха блока в руке), **ПКМ** — поставить блок.
- **Клавиша `E`** — творческая палитра из **22 блоков** (каменные и глиняные кирпичи, стекло, песчаник, книжные полки, руды, замшелый булыжник, снег, шерсть, светящийся камень и др.).
- **`1`..`9` или колесо мыши** — выбор слота хотбара.
- **Клавиша `T` или `/`** — живой мультиплеерный чат и серверные команды (`/help`, `/seed`, `/tp`, `/time set`).
- **Клавиша `F3`** — экран отладки поверх игры (FPS, XYZ, чанки, ОЗУ); **`Esc`** — меню паузы и сохранение.

---

## Завершённые итерации разработки

- **Шаги 1–8** — чистая модульная DAG-архитектура из 11 проектов (`<= 500` строк на файл, `TreatWarningsAsErrors=true`, 5 пакетов анализаторов), математика/реестры/NBT/Arch ECS, воксельный мир + регионы `.ohrg` + BFS-освещение, swept-AABB физика, инвентарь/крафт/3D A*, сетевой стек на `System.IO.Pipelines`, плагины и рендерер Silk.NET OpenGL 3.3 + OpenAL Soft.
- **Шаг 9 (`v0.1.0-alpha`)** — DDA рейкастер блоков, интерактивное строительство, синхронизация аватаров друзей и первые самодостаточные сборки.
- **Шаг 10 (`v0.2.0-alpha` — C# EDITION Upgrade)** — главное меню **«OVER THE HORIZON — C# EDITION»** на фоне 3D-панорамы, внутриигровые экраны **Multiplayer** (с хостингом сервера в 1 клик), **Options** и **Pause**, процедурный пиксельный 8×8 шрифт, 3D-блок в руке с анимацией покачивания и удара, солнце и плывущие облака, чёткий контур выделенного блока, палитра 22 блоков (`E`), живой чат (`T`), экран `F3` и 3D-ники над головами друзей.

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
