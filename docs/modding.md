# Modding API (`OverHorizon.Modding`)

> Расширяемость построена исключительно на явных контрактах и данных — **без байткод-трансформаций и Mixin**.

## 1. Жизненный цикл и фазы загрузки

1. **Сбор плагинов и датапаков**:
   - Статически/DI-зарегистрированные реализации `IModInitializer` (100 % совместимо с Native AOT).
   - JSON-датапаки `*.datapack.json` через `DataPackLoader` (`System.Text.Json` source generator, без рефлексии).
   - Опциональные внешние `.dll`-плагины через `PluginAssemblyLoader` на базе выгружаемого `CollectibleModLoadContext` (`isCollectible: true`, доступно в JIT-режиме).
2. **Топологическая сортировка (`ModDependencySorter`)**:
   - Проверка уникальности `ModManifest.Id`, наличия всех `Dependencies` и отсутствия циклов с детерминированным tie-breaking по ID.
3. **Регистрация контента (`IModRegistrationContext`)**:
   - До запуска рабочих потоков моды регистрируют блоки (+ автоматические block-items), предметы, типы сущностей и биомы.
   - Сразу после инициализации всех модов вызывается `Freeze()`, переводящий реестры в неизменяемые `FrozenDictionary` и плоские массивы для lock-free многопоточного доступа.

## 2. Типизированная шина событий (`IEventBus`)

- Все события (`ServerTickEvent`, `BlockBreakEvent`, `BlockPlaceEvent`, `PlayerChatEvent`) являются `struct`, передаваемыми по `ref` в `GameEventAction<TEvent>` (**0 байт аллокаций на публикацию**).
- Приоритеты: `Lowest -> Low -> Normal -> High -> Highest -> Monitor`.
- Поддержка отмены (`ICancellableEvent.IsCancelled`) и модификации полей события на лету; подписчики `EventPriority.Monitor` получают финальный результат независимо от флага `ignoreCancelled`.

## 3. Команды и права (`ICommandDispatcher`)

- Иерархия уровней доступа `PermissionLevel`: `Everyone (0) -> Moderator (1) -> GameMaster (2) -> Admin (3) -> Owner (4)`.
- Встроенные серверные команды (`BuiltInServerCommands`): `/help`, `/seed`, `/tp <x> <y> <z>`, `/setblock <x> <y> <z> <namespace:block>`, `/op <username>`.
- Моды добавляют собственные `/`-команды через `context.Commands.Register(...)`.

## 4. Формат JSON-датапака (`*.datapack.json`)

```json
{
  "manifest": {
    "id": "crystal_caverns",
    "name": "Crystal Caverns Pack",
    "version": "0.2.0",
    "dependencies": []
  },
  "blocks": [
    {
      "key": "crystal_caverns:crystal_ore",
      "isSolid": true,
      "isOpaque": true,
      "lightEmission": 12,
      "hardness": 3.0,
      "registerItem": true
    }
  ],
  "items": [
    {
      "key": "crystal_caverns:ruby_shard",
      "maxStackSize": 64
    }
  ],
  "biomes": [
    {
      "key": "crystal_caverns:crystal_valley",
      "temperature": 0.4,
      "downfall": 0.6,
      "surfaceBlock": "crystal_caverns:crystal_ore",
      "underSurfaceBlock": "overhorizon:stone"
    }
  ]
}
```
