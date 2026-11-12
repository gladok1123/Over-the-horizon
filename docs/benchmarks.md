# Benchmarks и цели

## Цели

- 20 TPS при 500+ игроках (при достаточном железе).
- <16 ms на кадр рендера (60 FPS).
- Chunk load <50 ms; lighting <5 ms/чанk; worldgen <100 ms/чанk.

## Методика

- BenchmarkDotNet для: chunk tick, lighting, worldgen, meshing, physics.
- Профилирование: dotTrace / PerfView / dotnet-trace.
## Реализованные наборы BenchmarkDotNet (`tests/OverHorizon.Benchmarks`)

- `ClockBenchmarks` — накладные расходы монотонных часов.
- `CoreAndEcsBenchmarks` — математика, интернированный `BlockState`, NBT и параллельные итерации Arch ECS.
- `WorldBenchmarks` — `PalettedContainer`, `OverworldChunkGenerator`, `LightingEngine`, `ChunkNbtSerializer`.
- `PhysicsAndGameplayBenchmarks` — `VoxelSweptCollider`, `FluidSimulator`, 3D A* `VoxelPathfinder`, тик ECS.
- `NetworkingBenchmarks` — `VarInt`, ZLib `PacketFramer`, `JavaProtocolCodec`, `ChunkNetworkSerializer`.
- `ModdingBenchmarks` — публикация событий `EventBus` (struct/zero-alloc) и парсинг команд `CommandDispatcher`.
- `RenderingBenchmarks` — `GreedySectionMesher`, `ParallelChunkMesher` (3×3 колонок), `Frustum` culling, процедурный атлас и PCM-синтез звуков.
