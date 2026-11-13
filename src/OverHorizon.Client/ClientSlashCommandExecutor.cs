using System.Globalization;
using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Mobs;

namespace OverHorizon.Client;

/// <summary>
/// Executes built-in console / chat slash-commands in Singleplayer (and client-side helpers):
/// <c>/time</c>, <c>/gamemode</c>, <c>/tp</c>, <c>/give</c>, <c>/clear</c>, <c>/kill</c>, <c>/summon</c>, <c>/seed</c>, <c>/setblock</c>, and <c>/help</c>.
/// </summary>
internal static class ClientSlashCommandExecutor
{
    public static bool TryExecuteLocalCommand(
        string rawInput,
        ClientPlayerController player,
        ClientWorldSyncAndMobBridge worldBridge,
        WorldMobManager mobManager,
        CoreRegistries registries,
        IAudioSubsystem audio,
        ClientChatHistory chatHistory,
        long currentSeed)
    {
        var trimmed = rawInput.Trim();
        if (!trimmed.StartsWith('/'))
        {
            return false;
        }

        var parts = trimmed[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return true;
        }

        var cmd = parts[0].ToLowerInvariant();
        switch (cmd)
        {
            case "help" or "?":
                chatHistory.AddMessage("[System] Commands: /time set <day|noon|sunset|night>, /gamemode <s|c|sp>, /effect <swiftness|regen|strength|night|clear>, /tp <x> <y> <z>, /give <item> [count], /summon <mob|tnt>, /clear, /kill, /seed");
                return true;

            case "effect":
                ExecuteEffectCommand(parts, player, chatHistory);
                return true;

            case "time":
                ExecuteTimeCommand(parts, worldBridge, chatHistory);
                return true;

            case "gamemode" or "gm":
                ExecuteGamemodeCommand(parts, player, chatHistory);
                return true;

            case "tp" or "teleport":
                ExecuteTeleportCommand(parts, player, chatHistory);
                return true;

            case "give":
                ExecuteGiveCommand(parts, player, registries.Items, audio, chatHistory);
                return true;

            case "clear":
                player.Hotbar.ClearEntireInventory();
                chatHistory.AddMessage("[System] Cleared inventory.");
                return true;

            case "kill":
                _ = player.ApplyDamage(1000.0f, audio);
                chatHistory.AddMessage("[System] Player eliminated.");
                return true;

            case "summon":
                ExecuteSummonCommand(parts, player, worldBridge, mobManager, audio, chatHistory);
                return true;

            case "seed":
                chatHistory.AddMessage($"[System] Seed: [{currentSeed.ToString(CultureInfo.InvariantCulture)}]");
                return true;

            case "setblock":
                ExecuteSetBlockCommand(parts, worldBridge, registries.Blocks, chatHistory);
                return true;

            default:
                chatHistory.AddMessage($"[System] Unknown command '/{cmd}'. Type /help for commands.");
                return true;
        }
    }

    private static void ExecuteTimeCommand(string[] parts, ClientWorldSyncAndMobBridge worldBridge, ClientChatHistory chatHistory)
    {
        if (parts.Length == 1 || string.Equals(parts[1], "query", StringComparison.OrdinalIgnoreCase))
        {
            chatHistory.AddMessage($"[System] The time is {worldBridge.TimeCycle.TimeOfDay.ToString(CultureInfo.InvariantCulture)} (Day {worldBridge.TimeCycle.DayNumber.ToString(CultureInfo.InvariantCulture)}).");
            return;
        }

        if (parts.Length >= 3 && string.Equals(parts[1], "set", StringComparison.OrdinalIgnoreCase))
        {
            if (WorldTimeCycle.TryParseTimeSpec(parts[2], out var ticks))
            {
                worldBridge.TimeCycle.SetTime(ticks);
                chatHistory.AddMessage($"[System] Set the time to {ticks.ToString(CultureInfo.InvariantCulture)} ({parts[2]}).");
            }
            else
            {
                chatHistory.AddMessage("[System] Usage: /time set <day|noon|sunset|night|midnight|ticks>");
            }

            return;
        }

        if (parts.Length >= 3 &&
            string.Equals(parts[1], "add", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var addTicks))
        {
            worldBridge.TimeCycle.AddTime(addTicks);
            chatHistory.AddMessage($"[System] Added {addTicks.ToString(CultureInfo.InvariantCulture)} ticks (Time: {worldBridge.TimeCycle.TimeOfDay.ToString(CultureInfo.InvariantCulture)}).");
            return;
        }

        chatHistory.AddMessage("[System] Usage: /time <set|add|query> [day|noon|sunset|night|midnight|ticks]");
    }

    private static void ExecuteGamemodeCommand(string[] parts, ClientPlayerController player, ClientChatHistory chatHistory)
    {
        var next = player.Mode switch
        {
            GameMode.Creative => GameMode.Survival,
            GameMode.Survival => GameMode.Spectator,
            _ => GameMode.Creative,
        };
        if (parts.Length >= 2)
        {
            var arg = parts[1].ToLowerInvariant();
            if (arg is "survival" or "s" or "0") next = GameMode.Survival;
            else if (arg is "creative" or "c" or "1") next = GameMode.Creative;
            else if (arg is "spectator" or "sp" or "3") next = GameMode.Spectator;
        }

        player.SetGameMode(next);
        chatHistory.AddMessage($"[System] Set own game mode to {next} Mode.");
    }

    private static void ExecuteEffectCommand(string[] parts, ClientPlayerController player, ClientChatHistory chatHistory)
    {
        if (parts.Length < 2 || string.Equals(parts[1], "clear", StringComparison.OrdinalIgnoreCase))
        {
            player.StatusEffects.Clear();
            chatHistory.AddMessage("[System] Cleared status effects.");
            return;
        }

        var eff = parts[1].ToLowerInvariant() switch
        {
            "swiftness" or "speed" => OverHorizon.Gameplay.Effects.StatusEffectType.Swiftness,
            "regen" or "regeneration" => OverHorizon.Gameplay.Effects.StatusEffectType.Regeneration,
            "strength" => OverHorizon.Gameplay.Effects.StatusEffectType.Strength,
            "night" or "night_vision" => OverHorizon.Gameplay.Effects.StatusEffectType.NightVision,
            _ => OverHorizon.Gameplay.Effects.StatusEffectType.Absorption,
        };
        player.StatusEffects.ApplyEffect(eff, 90.0f);
        chatHistory.AddMessage($"[System] Granted {eff} for 90s.");
    }

    private static void ExecuteTeleportCommand(string[] parts, ClientPlayerController player, ClientChatHistory chatHistory)
    {
        if (parts.Length < 4 ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
        {
            chatHistory.AddMessage("[System] Usage: /tp <x> <y> <z>");
            return;
        }

        player.ApplyAuthoritativePositionOnly(new Vec3d(x, y, z));
        chatHistory.AddMessage(string.Create(CultureInfo.InvariantCulture, $"[System] Teleported to ({x:F1}, {y:F1}, {z:F1})."));
    }

    private static void ExecuteGiveCommand(
        string[] parts,
        ClientPlayerController player,
        IRegistry<ItemDefinition> items,
        IAudioSubsystem audio,
        ClientChatHistory chatHistory)
    {
        if (parts.Length < 2)
        {
            chatHistory.AddMessage("[System] Usage: /give <item> [count]");
            return;
        }

        var rawKey = parts[1].Contains(':', StringComparison.Ordinal) ? parts[1] : $"overhorizon:{parts[1].ToLowerInvariant()}";
        if (!ResourceKey.TryParse(rawKey, out var itemKey) || !items.TryGet(itemKey, out var def))
        {
            chatHistory.AddMessage($"[System] Unknown item '{parts[1]}'.");
            return;
        }

        var count = 1;
        if (parts.Length >= 3 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCnt))
        {
            count = Math.Clamp(parsedCnt, 1, 64);
        }

        var id = (ushort)Math.Max(0, items.GetId(itemKey));
        _ = player.Hotbar.SurvivalInventory.AddItem(new ItemStack(id, count), items);
        player.Hotbar.RefreshSlotVisuals(player.Mode);
        audio.PlayPositional(SoundCue.ItemPop, player.Camera.Position, gain: 0.55f * player.MasterVolume);
        chatHistory.AddMessage($"[System] Gave {count.ToString(CultureInfo.InvariantCulture)}x [{def.EffectiveName}].");
    }

    private static void ExecuteSummonCommand(
        string[] parts,
        ClientPlayerController player,
        ClientWorldSyncAndMobBridge worldBridge,
        WorldMobManager mobManager,
        IAudioSubsystem audio,
        ClientChatHistory chatHistory)
    {
        if (parts.Length < 2)
        {
            chatHistory.AddMessage("[System] Usage: /summon <pig|cow|sheep|chicken|zombie|skeleton|spider|creeper|tnt>");
            return;
        }

        var f = player.Camera.Forward;
        var spawnPos = new Vec3d(player.FeetPosition.X + (f.X * 2.5), player.FeetPosition.Y + 0.2, player.FeetPosition.Z + (f.Z * 2.5));
        var kind = parts[1].ToLowerInvariant();
        if (kind is "tnt" or "primed_tnt")
        {
            _ = worldBridge.WorldTicker.SpawnPrimedTnt(spawnPos.ToBlockPos());
            audio.PlayPositional(SoundCue.TntFuse, spawnPos, gain: 0.85f * player.MasterVolume);
            chatHistory.AddMessage("[System] Summoned Primed TNT!");
            return;
        }

        MobType? mobType = kind switch
        {
            "pig" => MobType.Pig,
            "cow" => MobType.Cow,
            "sheep" => MobType.Sheep,
            "chicken" => MobType.Chicken,
            "zombie" => MobType.Zombie,
            "skeleton" => MobType.Skeleton,
            "spider" => MobType.Spider,
            "creeper" => MobType.Creeper,
            _ => null,
        };

        if (mobType is not { } mt)
        {
            chatHistory.AddMessage($"[System] Unknown entity '{parts[1]}'.");
            return;
        }

        _ = mobManager.SpawnMob(mt, spawnPos);
        chatHistory.AddMessage($"[System] Summoned new {mt}!");
    }

    private static void ExecuteSetBlockCommand(
        string[] parts,
        ClientWorldSyncAndMobBridge worldBridge,
        IBlockRegistry blocks,
        ClientChatHistory chatHistory)
    {
        if (parts.Length < 5 ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
            !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
        {
            chatHistory.AddMessage("[System] Usage: /setblock <x> <y> <z> <block>");
            return;
        }

        var rawKey = parts[4].Contains(':', StringComparison.Ordinal) ? parts[4] : $"overhorizon:{parts[4].ToLowerInvariant()}";
        if (!ResourceKey.TryParse(rawKey, out var bKey) || !blocks.TryGet(bKey, out var def))
        {
            chatHistory.AddMessage($"[System] Unknown block '{parts[4]}'.");
            return;
        }

        var ok = worldBridge.ApplyLocalBlockChangeWithCascade(new BlockPos(x, y, z), def.DefaultState, spawnDetachedItemDrops: false);
        chatHistory.AddMessage(ok ? $"[System] Set block at ({x}, {y}, {z}) to {bKey}." : "[System] Target chunk not loaded.");
    }
}
