# Протокол: Java + Bedrock (фазово)

> Реализация по документации minecraft.wiki. Код Mojang не копируется.

## Фаза 1 (MVP): Java-ядро + кастомный внутренний

- Handshake / status / login / play (Java-подобный).
- VarInt, пакеты через `System.IO.Pipelines`.
- Сериализация: source-generated `System.Text.Json` / MessagePack (внутренние), NBT-совместимый бинарный (мировые данные).
- Compression: zlib/gzip. Async I/O, backpressure.

## Фаза 2 (Beta): Bedrock

- Второй кодек (Bedrock-подобный), отдельные тесты и fuzz.
- Общий слой сессий/пакетов, два адаптера.

## Риски

- Два протокола = двойной объём кодеков и тестов. Митигация: общий слой абстракции пакетов, Bedrock только после стабильности Java-ядра.
