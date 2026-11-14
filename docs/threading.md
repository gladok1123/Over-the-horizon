# Многопоточность (Horizon-style)

## 1. Модель потоков

| Поток | Ответственность |
|---|---|
| Main thread | Приём сети + диспетчеризация пакетов; финальный merge (barrier); рендер (клиент) |
| Job System | Пул воркеров (ThreadPool + кастомный scheduler с приоритетами) |
| Chunk I/O | Чтение/запись с диска через `System.IO.Pipelines` |
| Lighting | Асинхронное распространение sky + block light |
| Worldgen | Параллельная генерация чанков (noise, features) |
| Entity AI | Параллельный тик AI по read-only snapshot мира |
| Network I/O | `System.Net.Sockets` + Pipelines |

## 2. Фазы тика (20 TPS)

```
Tick N:
  Phase 0:  Ingest network packets (main thread)
  Phase 1:  Parallel entity AI update (read snapshot)
  Phase 2:  Parallel block entity tick
  Phase 3:  Parallel chunk tick (random ticks, fluids, redstone)
  Phase 4:  Apply world mutations (main thread, deterministic order by ID)
  Phase 5:  Async lighting updates (queue, apply later)
  Phase 6:  Async chunk load/unload (Channels)
  Phase 7:  Send network packets (batched, main thread)
  Phase 8:  Save dirty chunks (async, background)
```

Между фазами — барьеры (`Barrier`/`CountdownEvent`). Внутри фазы — `Parallel.ForEachAsync` или job system.

## 3. Структуры и приёмы

- Lock-free очереди: `Channel<T>` для I/O, lighting, worldgen.
- `ConcurrentDictionary`/`ConcurrentBag` — только где нужны.
- Read-only snapshots: `ReadOnlySpan<T>`, `ImmutableArray<T>`, double buffering.
- Атомарность: `Interlocked`, `Volatile`, `MemoryBarrier`.
- SIMD: `Vector<T>`, `TensorPrimitives` для noise, физики, освещения.
- Память: `ArrayPool<T>`, `IMemoryOwner<T>`, пулы чанков/мешей; избегать LOH.
- Горячие пути: `struct`/`record struct`, `in`/`ref readonly`, `Span<T>`/`Memory<T>`, `stackalloc`.
- Никаких `lock` в hot-path; никаких `.Result`/`.Wait()`; никаких `async void` (кроме event handlers).
- Cancellation: `CancellationToken` везде, `IAsyncDisposable`, корректное завершение потоков.
- GC: `ServerGarbageCollection=true`, `GCSettings.LatencyMode` где нужно; без `GC.Collect()`.

## 4. Детерминизм

- Порядок мутаций в Phase 4 — детерминирован (по ID).
- RNG изолирован на чанк/сущность: `Xoroshiro128+` / `SplitMix64` с per-thread state.
- Никакого общего `Random` из нескольких потоков.
- Тест: запустить N тиков 100 раз — сравнить хеши мира.

## 5. Цели производительности

- 20 TPS при 500+ игроках (при достаточном железе).
- <16 ms на кадр рендера (60 FPS, клиент).
- Chunk load <50 ms; lighting <5 ms/чанk; worldgen <100 ms/чанk.
- Замеры: BenchmarkDotNet; профилирование: dotTrace / PerfView / dotnet-trace.

См. `benchmarks.md`.
