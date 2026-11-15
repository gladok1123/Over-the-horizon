# ADR 0006: BlockState — interned ushort ID + flyweight

## Context
В MC Java `BlockState` — тяжёлые объекты с дублированием. Нужна эффективная альтернатива для памяти и скорости.

## Decision
`BlockState` — `readonly struct` с `ushort Id`, structural sharing свойств; палитры с бит-упаковкой (`PalettedContainer<T>`). `ItemStack` — `readonly record struct` + copy-on-write.

## Consequences
- Плюс: меньше памяти, быстрее сравнения/хеши, проще сериализация.
- Минус: нужна аккуратная интернизация и генерация ID (source generators).
