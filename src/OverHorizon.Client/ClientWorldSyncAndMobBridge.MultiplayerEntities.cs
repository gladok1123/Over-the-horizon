using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Effects;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Rendering.Hud;
using OverHorizon.World.Lighting;

namespace OverHorizon.Client;

/// <summary>
/// Builds 3D render visuals for Mobs, Primed TNT, Minecarts, Ballistic Arrows, 3D Dropped Items,
/// Smooth 60 FPS Falling Blocks, World Particles, and HUD Status Effects across both Singleplayer and Multiplayer!
/// </summary>
internal sealed partial class ClientWorldSyncAndMobBridge
{
    private WorldEntitiesSnapshotPacket? _mpEntitiesSnapshot;
    private readonly List<HudStatusEffectVisual> _hudEffectsScratch = new(8);

    public void ClearMultiplayerEntitySnapshots() => _mpEntitiesSnapshot = null;

    public void ApplyMultiplayerEntitiesSnapshot(WorldEntitiesSnapshotPacket pkt, long localEntityId, ClientPlayerController player)
    {
        _mpEntitiesSnapshot = pkt;
        Span<(int Id, ushort ItemId, int Count, Vec3d Position, Vec3d Velocity)> dropSpan = pkt.DroppedItems.Count <= 64
            ? stackalloc (int, ushort, int, Vec3d, Vec3d)[pkt.DroppedItems.Count]
            : new (int, ushort, int, Vec3d, Vec3d)[pkt.DroppedItems.Count];
        for (var i = 0; i < pkt.DroppedItems.Count; i++)
        {
            dropSpan[i] = pkt.DroppedItems[i];
        }

        DroppedItems.ReplaceFromServerSnapshots(dropSpan);

        // Keep local rider synced to authoritative multiplayer Minecart!
        for (var i = 0; i < pkt.Minecarts.Count; i++)
        {
            var mc = pkt.Minecarts[i];
            if (mc.RiderEntityId == localEntityId && localEntityId != 0)
            {
                if (player.RidingMinecartId != mc.Id) player.MountMinecart(mc.Id);
                player.SyncMountedMinecartPose(mc.Pos + new Vec3d(0.0, 0.12, 0.0));
                return;
            }
        }
    }

    public IReadOnlyList<WorldMobVisual> BuildMobVisuals(ClientPlayerController player, bool isMultiplayer)
    {
        _mobVisualScratch.Clear();
        var heldTorch = player.Hotbar.CombinedHeldLightEmission;
        var skyMul = TimeCycle.SkyBrightness;

        if (isMultiplayer)
        {
            if (_mpEntitiesSnapshot is not { } snap) return _mobVisualScratch;
            for (var i = 0; i < snap.Mobs.Count; i++)
            {
                var m = snap.Mobs[i];
                var bright = SampleWorldBrightness(m.FeetPos, player.Camera.Position, heldTorch, skyMul);
                _mobVisualScratch.Add(new WorldMobVisual(
                    m.FeetPos, m.Yaw, m.WalkPhase, m.TypeByte, m.HurtFlash, m.CreeperFuse, (m.Flags & 1) != 0, bright, (m.Flags & 2) != 0));
            }

            return _mobVisualScratch;
        }

        var mobs = _mobManager.Mobs;
        for (var i = 0; i < mobs.Count; i++)
        {
            var m = mobs[i];
            var bright = SampleWorldBrightness(m.FeetPosition, player.Camera.Position, heldTorch, skyMul);
            _mobVisualScratch.Add(new WorldMobVisual(m.FeetPosition, m.YawDegrees, m.WalkPhase, (byte)m.Type, m.HurtFlashTimer, m.CreeperFuseTimer, m.IsSheared, bright, m.IsOnFire));
        }

        return _mobVisualScratch;
    }

    public IReadOnlyList<PrimedTntVisual> BuildPrimedTntVisuals(ClientPlayerController player, bool isMultiplayer)
    {
        _ = player;
        _tntVisualScratch.Clear();
        var skyMul = TimeCycle.SkyBrightness;

        if (isMultiplayer)
        {
            if (_mpEntitiesSnapshot is not { } snap) return _tntVisualScratch;
            for (var i = 0; i < snap.PrimedTnts.Count; i++)
            {
                var t = snap.PrimedTnts[i];
                var (sky, blk) = _worldCache.GetLight(t.FeetPos.ToBlockPos());
                var effSky = (byte)Math.Clamp((int)MathF.Round(sky * skyMul), 0, 15);
                _tntVisualScratch.Add(new PrimedTntVisual(t.FeetPos, t.FlashWhite, LightingEngine.LightLevelToBrightness(Math.Max(effSky, blk))));
            }

            return _tntVisualScratch;
        }

        var tnts = WorldTicker.PrimedTnts;
        for (var i = 0; i < tnts.Count; i++)
        {
            var t = tnts[i];
            var (sky, blk) = _worldCache.GetLight(t.FeetPosition.ToBlockPos());
            var effSky = (byte)Math.Clamp((int)MathF.Round(sky * skyMul), 0, 15);
            _tntVisualScratch.Add(new PrimedTntVisual(t.FeetPosition, t.IsFlashingWhite, LightingEngine.LightLevelToBrightness(Math.Max(effSky, blk))));
        }

        return _tntVisualScratch;
    }

    public IReadOnlyList<MinecartVisual> BuildMinecartVisuals(bool isMultiplayer)
    {
        _minecartVisualScratch.Clear();
        var skyMul = TimeCycle.SkyBrightness;

        if (isMultiplayer)
        {
            if (_mpEntitiesSnapshot is not { } snap) return _minecartVisualScratch;
            for (var i = 0; i < snap.Minecarts.Count; i++)
            {
                var c = snap.Minecarts[i];
                var (sky, blk) = _worldCache.GetLight(c.Pos.ToBlockPos());
                var effSky = (byte)Math.Clamp((int)MathF.Round(sky * skyMul), 0, 15);
                _minecartVisualScratch.Add(new MinecartVisual(c.Id, c.Pos, c.Yaw, c.Pitch, c.Wobble, LightingEngine.LightLevelToBrightness(Math.Max(effSky, blk))));
            }

            return _minecartVisualScratch;
        }

        var carts = Minecarts.Minecarts;
        for (var i = 0; i < carts.Count; i++)
        {
            var c = carts[i];
            var (sky, blk) = _worldCache.GetLight(c.FeetPosition.ToBlockPos());
            var effSky = (byte)Math.Clamp((int)MathF.Round(sky * skyMul), 0, 15);
            _minecartVisualScratch.Add(new MinecartVisual(c.Id, c.FeetPosition, c.YawDegrees, c.PitchDegrees, c.HurtWobbleTimer, LightingEngine.LightLevelToBrightness(Math.Max(effSky, blk))));
        }

        return _minecartVisualScratch;
    }

    public IReadOnlyList<ArrowProjectileVisual> BuildArrowVisuals(bool isMultiplayer)
    {
        _arrowVisualScratch.Clear();
        var skyMul = TimeCycle.SkyBrightness;

        if (isMultiplayer)
        {
            if (_mpEntitiesSnapshot is not { } snap) return _arrowVisualScratch;
            for (var i = 0; i < snap.Arrows.Count; i++)
            {
                var a = snap.Arrows[i];
                var (sky, blk) = _worldCache.GetLight(a.Pos.ToBlockPos());
                var effSky = (byte)Math.Clamp((int)MathF.Round(sky * skyMul), 0, 15);
                _arrowVisualScratch.Add(new ArrowProjectileVisual(
                    a.Id, a.Pos, a.Yaw, a.Pitch, a.Shake, LightingEngine.LightLevelToBrightness(Math.Max(effSky, blk)), (a.Flags & 2) != 0));
            }

            return _arrowVisualScratch;
        }

        var arrows = Arrows.Arrows;
        for (var i = 0; i < arrows.Count; i++)
        {
            var a = arrows[i];
            var (sky, blk) = _worldCache.GetLight(a.Position.ToBlockPos());
            var effSky = (byte)Math.Clamp((int)MathF.Round(sky * skyMul), 0, 15);
            _arrowVisualScratch.Add(new ArrowProjectileVisual(
                a.Id, a.Position, a.YawDegrees, a.PitchDegrees, a.ShakeTimer, LightingEngine.LightLevelToBrightness(Math.Max(effSky, blk)), a.IsCritical));
        }

        return _arrowVisualScratch;
    }

    public IReadOnlyList<DroppedItemVisual> BuildDroppedItemVisuals(ClientPlayerController player)
    {
        _droppedVisualScratch.Clear();
        var list = DroppedItems.Items;
        var heldTorch = player.Hotbar.CombinedHeldLightEmission;
        var skyMul = TimeCycle.SkyBrightness;

        for (var i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d.Stack.IsEmpty) continue;
            var def = _items.GetById(d.Stack.ItemId);
            var bright = SampleWorldBrightness(d.Position, player.Camera.Position, heldTorch, skyMul);
            var isBlockCube = def.PlacedBlockKey is { } bk &&
                              bk != BuiltInBlocks.Torch &&
                              bk != BuiltInBlocks.RedstoneTorch &&
                              bk != BuiltInBlocks.Lantern &&
                              bk != BuiltInBlocks.Rail &&
                              bk != BuiltInBlocks.PoweredRail &&
                              bk != BuiltInBlocks.RedstoneWire &&
                              bk != BuiltInBlocks.RedstoneRepeater &&
                              bk != BuiltInBlocks.RedstoneComparator;

            var topT = def.PlacedBlockKey is { } tbk ? _atlas.GetTileIndex(tbk, Direction.Up) : -1;
            var sideT = def.PlacedBlockKey is { } sbk ? _atlas.GetTileIndex(sbk, Direction.East) : -1;
            var itemT = _atlas.GetItemTileIndex(def.Key);
            var age = d.AgeSeconds > 0.001f ? d.AgeSeconds : _mpDropSpinClock + (d.Id * 0.37f);
            var bob = (MathF.Sin(age * 3.1f) + 1.0f) * 0.055f;
            var spin = age * 1.9f;

            _droppedVisualScratch.Add(new DroppedItemVisual(
                d.Position + new Vec3d(0.0, bob, 0.0),
                spin,
                d.Stack.Count,
                isBlockCube,
                topT,
                sideT,
                itemT,
                bright));
        }

        return _droppedVisualScratch;
    }

    public IReadOnlyList<FallingBlockVisual> BuildFallingBlockVisuals(ClientPlayerController player, bool isMultiplayer)
    {
        _fallingVisualScratch.Clear();
        var heldTorch = player.Hotbar.CombinedHeldLightEmission;
        var skyMul = TimeCycle.SkyBrightness;

        if (isMultiplayer)
        {
            if (_mpEntitiesSnapshot is not { } snap) return _fallingVisualScratch;
            for (var i = 0; i < snap.FallingBlocks.Count; i++)
            {
                var fb = snap.FallingBlocks[i];
                var bKey = _blocks.GetStateMetadata(new BlockState(fb.BlockStateId)).BlockKey;
                var pos = new Vec3d(fb.BlockX + 0.5, fb.CurrentY, fb.BlockZ + 0.5);
                var bright = SampleWorldBrightness(pos + new Vec3d(0.0, 0.5, 0.0), player.Camera.Position, heldTorch, skyMul);
                _fallingVisualScratch.Add(new FallingBlockVisual(
                    pos, _atlas.GetTileIndex(bKey, Direction.Up), _atlas.GetTileIndex(bKey, Direction.East), _atlas.GetTileIndex(bKey, Direction.Down), bright));
            }

            return _fallingVisualScratch;
        }

        var list = WorldTicker.FallingBlocks;
        for (var i = 0; i < list.Count; i++)
        {
            var fb = list[i];
            var bKey = _blocks.GetStateMetadata(fb.State).BlockKey;
            var pos = new Vec3d(fb.BlockX + 0.5, fb.CurrentY, fb.BlockZ + 0.5);
            var bright = SampleWorldBrightness(pos + new Vec3d(0.0, 0.5, 0.0), player.Camera.Position, heldTorch, skyMul);
            _fallingVisualScratch.Add(new FallingBlockVisual(
                pos, _atlas.GetTileIndex(bKey, Direction.Up), _atlas.GetTileIndex(bKey, Direction.East), _atlas.GetTileIndex(bKey, Direction.Down), bright));
        }

        return _fallingVisualScratch;
    }

    public IReadOnlyList<WorldParticleVisual> BuildParticleVisuals()
    {
        _particleVisualScratch.Clear();
        var active = Particles.ActiveParticles;
        for (var i = 0; i < active.Length; i++)
        {
            ref readonly var p = ref active[i];
            var alpha = Math.Clamp(1.0f - (p.Age / Math.Max(0.05f, p.MaxLifetime)), 0.08f, 1.0f);
            _particleVisualScratch.Add(new WorldParticleVisual(
                p.Position,
                p.Size,
                new Vector4(p.Color, alpha),
                p.AtlasTileIndex,
                p.SubUvOffset.X,
                p.SubUvOffset.Y));
        }

        return _particleVisualScratch;
    }

    public IReadOnlyList<HudStatusEffectVisual> BuildHudStatusEffects(ClientPlayerController player)
    {
        _hudEffectsScratch.Clear();
        var active = player.StatusEffects.ActiveEffects;
        for (var i = 0; i < active.Count; i++)
        {
            var eff = active[i];
            _hudEffectsScratch.Add(new HudStatusEffectVisual(
                StatusEffectTracker.GetDisplayName(eff.Type),
                (int)MathF.Ceiling(eff.RemainingSeconds),
                StatusEffectTracker.GetIconColor(eff.Type)));
        }

        return _hudEffectsScratch;
    }

    private float SampleWorldBrightness(Vec3d pos, Vec3d camPos, byte heldTorch, float skyMul)
    {
        var (sky, blk) = _worldCache.GetLight(pos.ToBlockPos());
        var effSky = (byte)Math.Clamp((int)MathF.Round(sky * skyMul), 0, 15);
        var dist = (float)pos.DistanceTo(camPos);
        var torch = heldTorch > dist ? (byte)Math.Clamp((int)MathF.Round(heldTorch - dist), 0, 15) : (byte)0;
        return LightingEngine.LightLevelToBrightness(LightingEngine.ComputeEffectiveLightLevel(effSky, blk, torch));
    }
}
