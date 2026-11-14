# Протокол: Java + Bedrock (фазово)

> Реализация по открытой документации `minecraft.wiki`. Код Mojang не копируется.

## Фаза 1 (MVP `v0.1.0-alpha`): Java-ядро + мультиплеерная синхронизация игроков

- Состояния соединения: `Handshaking` → `Status` / `Login` → `Play` (`CurrentProtocolVersion = 767`).
- Кадрирование пакетов через `VarInt` + `PacketFramer` на базе `System.IO.Pipelines` с опциональным ZLib-сжатием и жёстким лимитом декомпрессии против zip-bomb.
- Source-generated `System.Text.Json` для ответа `StatusResponsePacket` (AOT-совместимо).

### Таблица пакетов состояния `Play` (`JavaProtocolCodec`)

#### Serverbound (Клиент → Сервер)
| ID | Пакет | Назначение |
| --- | --- | --- |
| `0x00` | `KeepAliveResponsePacket` | Ответ на пинг удержания соединения |
| `0x01` | `MovePlayerPacket` | Позиция ног `(X, Y, Z)`, углы `Yaw`/`Pitch`, флаг `OnGround` (проходит серверную swept-AABB античит-валидацию) |
| `0x02` | `PlayerBlockActionPacket` | Ломание (`BreakBlock`) и установка (`PlaceBlock`) блоков с проверкой дистанции (≤ 6 блоков) |
| `0x03` | `ChatMessagePacket` | Сообщения чата и серверные команды (`/help`, `/seed`, `/tp`, `/setblock`, `/op`) |

#### Clientbound (Сервер → Клиент)
| ID | Пакет | Назначение |
| --- | --- | --- |
| `0x00` | `DisconnectPacket` | Корректное отключение с причиной |
| `0x01` | `KeepAliveRequestPacket` | Периодический keep-alive запрос |
| `0x02` | `SynchronizePlayerPositionPacket` | Авторитативный спавн / телепорт / откат позиции игрока |
| `0x03` | `BlockUpdatePacket` | Мгновенная репликация сломанного или поставленного блока всем игрокам |
| `0x04` | `ChunkDataPacket` | Потоковая передача чанка (`ChunkNetworkSerializer`) |
| `0x05` | `SystemChatPacket` | Системные ответы команд и широковещательный чат |
| `0x06` | `RemotePlayerSpawnPacket` | Появление 3D-аватара друга в мире (`EntityId`, `Username`, поза) |
| `0x07` | `RemotePlayerMovePacket` | Синхронизация движения, поворота головы и фазы шага друга |
| `0x08` | `RemotePlayerDespawnPacket` | Удаление 3D-аватара вышедшего игрока |

## Фаза 2 (Beta): Bedrock

- Второй кодек (Bedrock-подобный поверх UDP/RakNet-слоя), отдельные тесты и fuzz-корпус.
- Общий слой игровых сессий `IServerPlayHandler`, два транспортных адаптера.
