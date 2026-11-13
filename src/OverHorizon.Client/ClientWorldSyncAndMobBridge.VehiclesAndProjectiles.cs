using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Rendering.Hud;
using OverHorizon.World.Lighting;

namespace OverHorizon.Client;

/// <summary>
/// World tick orchestration &amp; render-visual builders for Mobs, Minecarts, Ballistic Arrows, and Primed TNT.
/// </summary>
internal sealed partial class ClientWorldSyncAndMobBridge
{
    public void StepWorldSimulationAndMobs(ClientPlayerController player, float dt, float riderSteerForward = 0.0f)
    {
        TimeCycle.AdvanceSeconds(dt);
        var skyBright = TimeCycle.SkyBrightness;

        _editedPositionsScratch.Clear();
        _touchedChunksScratch.Clear();
        _ignitedTntScratch.Clear();
        _redstoneSoundScratch.Clear();
        _detonatedScratch.Clear();
        _creeperDetonations.Clear();
        _mobFallScratch.Clear();
        _arrowDropScratch.Clear();
        _arrowHitSoundScratch.Clear();

        // 1. Step Redstone (buttons, pressure plates, repeaters, pistons, powered rails, TNT ignition)
        var mobCount = _mobManager.Mobs.Count;
        var cartCount = Minecarts.Minecarts.Count;
        var totalFeet = 1 + mobCount + cartCount;
        Span<Vec3d> entityFeet = totalFeet <= 48 ? stackalloc Vec3d[totalFeet] : new Vec3d[totalFeet];
        entityFeet[0] = player.FeetPosition;
        for (var i = 0; i < mobCount; i++) entityFeet[1 + i] = _mobManager.Mobs[i].FeetPosition;
        for (var i = 0; i < cartCount; i++) entityFeet[1 + mobCount + i] = Minecarts.Minecarts[i].FeetPosition;

        Redstone.Step(dt, entityFeet, _localWorld.GetBlockState, RecordWorldSet, _ignitedTntScratch, _redstoneSoundScratch);

        // 2. Step Primed TNT (batch crater updates!), Gravity Blocks, Smooth 4-Block Water Flow, and Grass Spread
        WorldTicker.Step(
            dt, _worldCache, player.FeetPosition, _localWorld.GetBlockState, _worldCache.GetLight, RecordWorldSet, _detonatedScratch, RecordWorldBatchSet);

        // 3. Step Minecarts along straight, curved, sloped, and Redstone-energized Powered Rails
        if (player.RidingMinecartId < 0 && Minecarts.RiddenMinecart is not null)
        {
            _ = Minecarts.DismountPlayer(out _);
        }

        Minecarts.Step(dt, _worldCache, _localWorld.GetBlockState, player.FeetPosition, player.Camera.HorizontalForward, riderSteerForward);
        if (player.RidingMinecartId >= 0)
        {
            var ridden = Minecarts.RiddenMinecart;
            if (ridden is not null && ridden.Id == player.RidingMinecartId)
            {
                player.SyncMountedMinecartPose(ridden.FeetPosition + new Vec3d(0.0, 0.12, 0.0));
            }
            else
            {
                player.DismountMinecart();
            }
        }

        StepMinecartAudio(player, dt);

        // 4. Step Peaceful & Hostile Mobs (with mob fall damage, Chicken slow glide, and Skeleton ranged Bow arrows!)
        var mobDamage = _mobManager.Step(
            _worldCache,
            ProbeMobSpawnCell,
            player.FeetPosition,
            player.Mode,
            player.Hotbar.TotalArmorDefense,
            skyBright,
            dt,
            _creeperDetonations,
            onSkeletonShootArrow: (origin, vel) =>
            {
                _ = Arrows.SpawnSkeletonArrow(origin, vel);
                _audio.PlayPositional(SoundCue.BowShoot, origin, gain: 0.78f * player.MasterVolume, pitch: 0.94f);
            },
            mobLandingHurtEvents: _mobFallScratch);

        if (mobDamage > 0.0f) _ = player.ApplyDamage(mobDamage, _audio);
        for (var i = 0; i < _mobFallScratch.Count; i++)
        {
            var ev = _mobFallScratch[i];
            _audio.PlayPositional(ev.FallDamage > 5.0f ? SoundCue.FallBig : SoundCue.FallSmall, ev.Position, gain: 0.55f * player.MasterVolume);
            _audio.PlayPositional(ClientSurvivalCombatController.ResolveMobHurtOrDeathCue(ev.Type, ev.Died), ev.Position, gain: 0.75f * player.MasterVolume);
        }

        // 5. Step 3D Ballistic Arrows (voxel sticking, mob hits + loot, player hits, and ground arrow recovery!)
        var arrowDmgToPlayer = Arrows.Step(
            dt, _worldCache, _localWorld.GetBlockState, _mobManager, _items, player.FeetPosition, player.Mode, _arrowDropScratch, _arrowHitSoundScratch);
        if (arrowDmgToPlayer > 0.0f)
        {
            _ = player.ApplyDamage(arrowDmgToPlayer, _audio);
        }

        for (var i = 0; i < _arrowHitSoundScratch.Count; i++)
        {
            _audio.PlayPositional(SoundCue.ArrowHit, _arrowHitSoundScratch[i], gain: 0.75f * player.MasterVolume);
        }

        if (_arrowDropScratch.Count > 0 && player.Mode == GameMode.Survival)
        {
            for (var i = 0; i < _arrowDropScratch.Count; i++)
            {
                if (!_arrowDropScratch[i].IsEmpty)
                {
                    _ = player.Hotbar.SurvivalInventory.AddItem(_arrowDropScratch[i], _items);
                }
            }

            player.Hotbar.RefreshSlotVisuals(player.Mode);
            _audio.PlayPositional(SoundCue.ItemPop, player.Camera.Position, gain: 0.48f * player.MasterVolume);
        }

        // 6. Creeper & TNT Explosion Blasts
        for (var c = 0; c < _creeperDetonations.Count; c++)
        {
            _ = WorldTicker.DetonateRaycastExplosion(_creeperDetonations[c], 3.0f, _localWorld.GetBlockState, RecordWorldSet, RecordWorldBatchSet);
            _detonatedScratch.Add(_creeperDetonations[c]);
        }

        for (var e = 0; e < _detonatedScratch.Count; e++)
        {
            var boomCenter = _detonatedScratch[e];
            _audio.PlayPositional(SoundCue.Explosion, boomCenter, gain: 1.0f * player.MasterVolume);
            _ = _mobManager.ApplyExplosionBlast(boomCenter);

            var dist = player.FeetPosition.DistanceTo(boomCenter);
            if (dist < 8.0 && player.Mode == GameMode.Survival)
            {
                var exp = (float)Math.Clamp(1.0 - (dist / 8.0), 0.0, 1.0);
                var dmg = ((exp * exp) + exp) * 14.0f;
                _ = player.ApplyDamage(dmg, _audio);
            }
        }

        ProcessRedstoneOutputsAndRemesh();
        StepAmbientMobAudio(player, dt);
    }

    public IReadOnlyList<WorldMobVisual> BuildMobVisuals(ClientPlayerController player)
    {
        _mobVisualScratch.Clear();
        var mobs = _mobManager.Mobs;
        var heldTorch = player.Hotbar.CombinedHeldLightEmission;
        var skyMul = TimeCycle.SkyBrightness;
        for (var i = 0; i < mobs.Count; i++)
        {
            var m = mobs[i];
            var (sky, blk) = _worldCache.GetLight(m.FeetPosition.ToBlockPos());
            var effSky = (byte)Math.Clamp((int)MathF.Round(sky * skyMul), 0, 15);
            var distToPlayer = (float)m.FeetPosition.DistanceTo(player.Camera.Position);
            var torchAtMob = heldTorch > distToPlayer ? (byte)Math.Clamp((int)MathF.Round(heldTorch - distToPlayer), 0, 15) : (byte)0;
            var bright = LightingEngine.LightLevelToBrightness(LightingEngine.ComputeEffectiveLightLevel(effSky, blk, torchAtMob));
            _mobVisualScratch.Add(new WorldMobVisual(m.FeetPosition, m.YawDegrees, m.WalkPhase, (byte)m.Type, m.HurtFlashTimer, m.CreeperFuseTimer, m.IsSheared, bright, m.IsOnFire));
        }

        return _mobVisualScratch;
    }

    public IReadOnlyList<PrimedTntVisual> BuildPrimedTntVisuals(ClientPlayerController player)
    {
        _ = player;
        _tntVisualScratch.Clear();
        var tnts = WorldTicker.PrimedTnts;
        var skyMul = TimeCycle.SkyBrightness;
        for (var i = 0; i < tnts.Count; i++)
        {
            var t = tnts[i];
            var (sky, blk) = _worldCache.GetLight(t.FeetPosition.ToBlockPos());
            var effSky = (byte)Math.Clamp((int)MathF.Round(sky * skyMul), 0, 15);
            _tntVisualScratch.Add(new PrimedTntVisual(t.FeetPosition, t.IsFlashingWhite, LightingEngine.LightLevelToBrightness(Math.Max(effSky, blk))));
        }

        return _tntVisualScratch;
    }

    public IReadOnlyList<MinecartVisual> BuildMinecartVisuals()
    {
        _minecartVisualScratch.Clear();
        var carts = Minecarts.Minecarts;
        var skyMul = TimeCycle.SkyBrightness;
        for (var i = 0; i < carts.Count; i++)
        {
            var c = carts[i];
            var (sky, blk) = _worldCache.GetLight(c.FeetPosition.ToBlockPos());
            var effSky = (byte)Math.Clamp((int)MathF.Round(sky * skyMul), 0, 15);
            var bright = LightingEngine.LightLevelToBrightness(Math.Max(effSky, blk));
            _minecartVisualScratch.Add(new MinecartVisual(c.Id, c.FeetPosition, c.YawDegrees, c.PitchDegrees, c.HurtWobbleTimer, bright));
        }

        return _minecartVisualScratch;
    }

    public IReadOnlyList<ArrowProjectileVisual> BuildArrowVisuals()
    {
        _arrowVisualScratch.Clear();
        var arrows = Arrows.Arrows;
        var skyMul = TimeCycle.SkyBrightness;
        for (var i = 0; i < arrows.Count; i++)
        {
            var a = arrows[i];
            var (sky, blk) = _worldCache.GetLight(a.Position.ToBlockPos());
            var effSky = (byte)Math.Clamp((int)MathF.Round(sky * skyMul), 0, 15);
            var bright = LightingEngine.LightLevelToBrightness(Math.Max(effSky, blk));
            _arrowVisualScratch.Add(new ArrowProjectileVisual(a.Id, a.Position, a.YawDegrees, a.PitchDegrees, a.ShakeTimer, bright, a.IsCritical));
        }

        return _arrowVisualScratch;
    }

    private void StepMinecartAudio(ClientPlayerController player, float dt)
    {
        _minecartRollAudioTimer += dt;
        if (_minecartRollAudioTimer < 0.42 || Minecarts.Minecarts.Count == 0) return;
        _minecartRollAudioTimer = 0.0;

        for (var i = 0; i < Minecarts.Minecarts.Count; i++)
        {
            var c = Minecarts.Minecarts[i];
            var speed = c.Velocity.Length;
            if (c.IsOnRail && speed > 0.35 && c.FeetPosition.DistanceTo(player.Camera.Position) < 22.0)
            {
                var pitch = (float)Math.Clamp(0.82 + (speed * 0.045), 0.80, 1.25);
                _audio.PlayPositional(SoundCue.MinecartRoll, c.FeetPosition, gain: 0.36f * player.MasterVolume, pitch: pitch);
                break;
            }
        }
    }

    private void StepAmbientMobAudio(ClientPlayerController player, float dt)
    {
        _ambientMobSoundTimer += dt;
        if (_ambientMobSoundTimer < 4.2 || _mobManager.Mobs.Count == 0) return;
        _ambientMobSoundTimer = 0.0;

        var idx = (int)((uint)Environment.TickCount % (uint)_mobManager.Mobs.Count);
        var m = _mobManager.Mobs[idx];
        if (m.FeetPosition.DistanceTo(player.Camera.Position) > 24.0) return;

        var cue = m.Type switch
        {
            MobType.Pig => SoundCue.MobPigSay,
            MobType.Cow => SoundCue.MobCowSay,
            MobType.Sheep => SoundCue.MobSheepSay,
            MobType.Chicken => SoundCue.MobChickenSay,
            MobType.Zombie => SoundCue.MobZombieSay,
            MobType.Skeleton => SoundCue.MobSkeletonSay,
            MobType.Spider => SoundCue.MobSpiderSay,
            _ => SoundCue.MobCreeperHiss,
        };
        _audio.PlayPositional(cue, m.FeetPosition, gain: 0.65f * player.MasterVolume);
    }

    private (byte SkyLight, byte BlockLight, bool IsGrassTop, bool IsSolidGround, bool IsClearSpace) ProbeMobSpawnCell(BlockPos feetBlock)
    {
        if (!_worldCache.IsColumnLoaded(feetBlock)) return (15, 0, false, false, false);

        var below = new BlockPos(feetBlock.X, feetBlock.Y - 1, feetBlock.Z);
        var head = new BlockPos(feetBlock.X, feetBlock.Y + 1, feetBlock.Z);
        var belowKey = _blocks.GetStateMetadata(_worldCache.GetBlockState(below)).BlockKey;
        var feetDef = _blocks.GetRequired(_blocks.GetStateMetadata(_worldCache.GetBlockState(feetBlock)).BlockKey);
        var headDef = _blocks.GetRequired(_blocks.GetStateMetadata(_worldCache.GetBlockState(head)).BlockKey);
        var (sky, blk) = _worldCache.GetLight(feetBlock);

        var isSolidBelow = _blocks.GetRequired(belowKey).IsSolid;
        var isClear = !feetDef.IsSolid && !feetDef.IsFluid && !headDef.IsSolid && !headDef.IsFluid;
        return (sky, blk, belowKey == BuiltInBlocks.GrassBlock, isSolidBelow, isClear);
    }
}
