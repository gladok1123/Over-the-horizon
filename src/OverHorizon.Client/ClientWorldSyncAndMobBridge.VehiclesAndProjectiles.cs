using OverHorizon.Audio;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Effects;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Survival;
using OverHorizon.World.Lighting;

namespace OverHorizon.Client;

/// <summary>
/// Singleplayer simulation step for Redstone, Primed TNT &amp; Fuse Hiss, Smooth Falling Blocks, 3D Dropped Items,
/// A* Mobs, Shield Blocking, Minecarts, Ballistic Arrows, Fire Crackle, and 3D Particle Effects.
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
        _creeperFuseStarts.Clear();
        _shieldBlockPositions.Clear();
        _mobFallScratch.Clear();
        _arrowDropScratch.Clear();
        _arrowHitSoundScratch.Clear();
        _brokenFragileScratch.Clear();
        _extinguishedFireScratch.Clear();

        // Survival Death Drops: scatter all inventory items as 3D DroppedItems at death spot!
        if (player.IsDead && !player.DeathDropsExtracted)
        {
            player.DeathDropsExtracted = true;
            var deathStacks = player.Hotbar.ExtractAndClearInventoryForDeathDrop();
            for (var i = 0; i < deathStacks.Count; i++)
            {
                _ = DroppedItems.SpawnScatterDrop(player.FeetPosition, deathStacks[i], pickupDelaySeconds: 1.0f);
            }
        }

        // 1. Step Redstone (wire, repeaters, comparators, buttons, pressure plates, pistons, trapdoors, powered rails, TNT)
        var mobCount = _mobManager.Mobs.Count;
        var cartCount = Minecarts.Minecarts.Count;
        var totalFeet = 1 + mobCount + cartCount;
        Span<Vec3d> entityFeet = totalFeet <= 48 ? stackalloc Vec3d[totalFeet] : new Vec3d[totalFeet];
        entityFeet[0] = player.Mode == GameMode.Spectator ? new Vec3d(0, -9999, 0) : player.FeetPosition;
        for (var i = 0; i < mobCount; i++) entityFeet[1 + i] = _mobManager.Mobs[i].FeetPosition;
        for (var i = 0; i < cartCount; i++) entityFeet[1 + mobCount + i] = Minecarts.Minecarts[i].FeetPosition;

        Redstone.Step(dt, entityFeet, _localWorld.GetBlockState, RecordWorldSet, _ignitedTntScratch, _redstoneSoundScratch);

        // 2. Step Primed TNT, Smooth 60 FPS Falling Sand/Gravel, Controlled Fire, and Batched Water
        WorldTicker.Step(
            dt,
            _worldCache,
            player.FeetPosition,
            _localWorld.GetBlockState,
            _worldCache.GetLight,
            RecordWorldSet,
            _detonatedScratch,
            RecordWorldBatchSet,
            _brokenFragileScratch,
            _extinguishedFireScratch);

        for (var i = 0; i < _brokenFragileScratch.Count; i++)
        {
            var (bPos, bState) = _brokenFragileScratch[i];
            var bKey = _blocks.GetStateMetadata(bState).BlockKey;
            var drop = BlockDropResolver.ResolveDrop(bKey, heldTool: null, _items, _dropSeq++);
            if (!drop.IsEmpty) _ = DroppedItems.SpawnBlockDrop(bPos, drop);
        }

        for (var i = 0; i < _extinguishedFireScratch.Count; i++)
        {
            var fPos = _extinguishedFireScratch[i];
            var fc = new Vec3d(fPos.X + 0.5, fPos.Y + 0.25, fPos.Z + 0.5);
            _audio.PlayPositional(SoundCue.Fizz, fc, gain: 0.55f * player.MasterVolume);
            Particles.EmitFlameAndSmoke(fc, smokeOnly: true);
        }

        // 3. Step 3D Dropped Items (gravity, bounce, stack merge, magnetic pickup, and ItemPop audio!)
        var pickedUp = DroppedItems.Step(
            dt,
            _worldCache,
            _items,
            player.IsDead ? null : player.Hotbar.SurvivalInventory,
            player.FeetPosition,
            player.Mode);
        if (pickedUp > 0)
        {
            player.Hotbar.RefreshSlotVisuals(player.Mode);
            _audio.PlayPositional(SoundCue.ItemPop, player.Camera.Position, gain: 0.55f * player.MasterVolume, pitch: 1.05f);
        }

        // 4. Step Minecarts
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

        // 5. Step A* Mobs (with Creeper Fuse Hiss, Shield deflection, and Skeleton ranged Bow arrows!)
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
            mobLandingHurtEvents: _mobFallScratch,
            creeperFuseStartedPositions: _creeperFuseStarts,
            isPlayerBlockingShield: player.IsBlockingShield,
            playerLookForward: player.Camera.HorizontalForward,
            shieldBlockedPositions: _shieldBlockPositions,
            isFireBlock: bp => _blocks.GetStateMetadata(_localWorld.GetBlockState(bp)).BlockKey == BuiltInBlocks.Fire);

        for (var i = 0; i < _creeperFuseStarts.Count; i++)
        {
            _audio.PlayPositional(SoundCue.MobCreeperHiss, _creeperFuseStarts[i], gain: 0.95f * player.MasterVolume, pitch: 1.0f);
            Particles.EmitFlameAndSmoke(_creeperFuseStarts[i] + new Vec3d(0, 1.35, 0), smokeOnly: true);
        }

        if (mobDamage > 0.0f) _ = player.ApplyDamage(mobDamage, _audio);
        for (var i = 0; i < _mobFallScratch.Count; i++)
        {
            var ev = _mobFallScratch[i];
            if (ev.FallDamage < 0.0f)
            {
                _audio.PlayPositional(SoundCue.Fizz, ev.Position, gain: 0.36f * player.MasterVolume, pitch: 1.12f);
                _audio.PlayPositional(ClientSurvivalCombatController.ResolveMobHurtOrDeathCue(ev.Type, ev.Died), ev.Position, gain: 0.72f * player.MasterVolume);
                continue;
            }

            _audio.PlayPositional(ev.FallDamage > 5.0f ? SoundCue.FallBig : SoundCue.FallSmall, ev.Position, gain: 0.55f * player.MasterVolume);
            _audio.PlayPositional(ClientSurvivalCombatController.ResolveMobHurtOrDeathCue(ev.Type, ev.Died), ev.Position, gain: 0.75f * player.MasterVolume);
        }

        // 6. Step 3D Ballistic Arrows (with Shield deflection & 3D loot drops!)
        var arrowDmgToPlayer = Arrows.Step(
            dt,
            _worldCache,
            _localWorld.GetBlockState,
            _mobManager,
            _items,
            player.FeetPosition,
            player.Mode,
            _arrowDropScratch,
            _arrowHitSoundScratch,
            player.IsBlockingShield,
            player.Camera.HorizontalForward,
            _shieldBlockPositions);
        if (arrowDmgToPlayer > 0.0f) _ = player.ApplyDamage(arrowDmgToPlayer, _audio);

        for (var i = 0; i < _shieldBlockPositions.Count; i++)
        {
            _audio.PlayPositional(SoundCue.ShieldBlock, player.Camera.Position, gain: 0.88f * player.MasterVolume);
        }

        for (var i = 0; i < _arrowHitSoundScratch.Count; i++)
        {
            _audio.PlayPositional(SoundCue.ArrowHit, _arrowHitSoundScratch[i], gain: 0.75f * player.MasterVolume);
        }

        if (_arrowDropScratch.Count > 0 && player.Mode != GameMode.Spectator)
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

        // 7. Creeper & TNT Explosion Blasts + Shield Blast Mitigation + Explosion Particles!
        for (var c = 0; c < _creeperDetonations.Count; c++)
        {
            _ = WorldTicker.DetonateRaycastExplosion(_creeperDetonations[c], 3.0f, _localWorld.GetBlockState, RecordWorldSet, RecordWorldBatchSet);
            _detonatedScratch.Add(_creeperDetonations[c]);
        }

        for (var e = 0; e < _detonatedScratch.Count; e++)
        {
            var boomCenter = _detonatedScratch[e];
            _audio.PlayPositional(SoundCue.Explosion, boomCenter, gain: 1.0f * player.MasterVolume);
            Particles.EmitExplosion(boomCenter);
            _ = _mobManager.ApplyExplosionBlast(boomCenter);

            var dist = player.FeetPosition.DistanceTo(boomCenter);
            if (dist < 8.0 && player.Mode == GameMode.Survival)
            {
                var exp = (float)Math.Clamp(1.0 - (dist / 8.0), 0.0, 1.0);
                var dmg = ((exp * exp) + exp) * 14.0f;
                if (player.TryDeflectWithShield(boomCenter, _audio))
                {
                    dmg *= 0.15f;
                }

                if (dmg >= 0.5f) _ = player.ApplyDamage(dmg, _audio);
            }
        }

        ProcessRedstoneOutputsAndRemesh();
        StepAmbientMobAudio(player, dt);
        StepWorldParticlesAndFuseAudio(player, dt);
    }

    public void StepWorldParticlesAndFuseAudio(ClientPlayerController player, float dt)
    {
        _mpDropSpinClock += dt;
        Particles.Step(dt);

        _particleTickTimer += dt;
        if (_particleTickTimer >= 0.09)
        {
            _particleTickTimer = 0.0;

            // Sprint ground dust
            if (player.IsSprinting && player.OnGround && !player.InFluid)
            {
                var under = new BlockPos((int)Math.Floor(player.FeetPosition.X), (int)Math.Floor(player.FeetPosition.Y - 0.2), (int)Math.Floor(player.FeetPosition.Z));
                var uState = _worldCache.GetBlockState(under);
                if (!uState.IsAir)
                {
                    var uKey = _blocks.GetStateMetadata(uState).BlockKey;
                    Particles.EmitBlockBreak(under, _atlas.GetTileIndex(uKey, Direction.Up), ClientHotbar.ResolveFallbackColor(uKey), baseCount: 2);
                }
            }

            // Primed TNT & Creeper active fuse smoke
            var tnts = WorldTicker.PrimedTnts;
            for (var i = 0; i < tnts.Count; i++)
            {
                Particles.EmitFlameAndSmoke(tnts[i].FeetPosition + new Vec3d(0.0, 0.98, 0.0));
            }

            var mobs = _mobManager.Mobs;
            for (var i = 0; i < mobs.Count; i++)
            {
                if (mobs[i].CreeperFuseTimer > 0.01f)
                {
                    Particles.EmitFlameAndSmoke(mobs[i].FeetPosition + new Vec3d(0.0, 1.45, 0.0), smokeOnly: true);
                }
            }

            // Void motes near/below Bedrock
            if (player.FeetPosition.Y < EngineConstants.WorldMinY + 6.0)
            {
                Particles.EmitBurst(player.FeetPosition + new Vec3d(0.0, 1.0, 0.0), ParticleKind.VoidMote, 3);
            }
        }

        // Active Fire crackle (PrimedTnt plays SoundCue.TntFuse once at ignition — no duplicate 0.65s re-trigger!)
        _ = _tntFuseAudioTimer;

        if (WorldTicker.ActiveFirePositions.Count > 0)
        {
            _fireCrackleTimer += dt;
            if (_fireCrackleTimer >= 0.55)
            {
                _fireCrackleTimer = 0.0;
                foreach (var fp in WorldTicker.ActiveFirePositions)
                {
                    var fc = new Vec3d(fp.X + 0.5, fp.Y + 0.25, fp.Z + 0.5);
                    if (fc.DistanceTo(player.Camera.Position) <= 18.0)
                    {
                        Particles.EmitFlameAndSmoke(fc);
                        _audio.PlayPositional(SoundCue.FireCrackle, fc, gain: 0.42f * player.MasterVolume);
                        break;
                    }
                }
            }
        }
    }

    private void StepMinecartAudio(ClientPlayerController player, float dt)
    {
        _minecartRollAudioTimer += dt;
        if (_minecartRollAudioTimer < 0.48) return;
        _minecartRollAudioTimer = 0.0;
        var carts = Minecarts.Minecarts;
        for (var i = 0; i < carts.Count; i++)
        {
            var vel = carts[i].Velocity;
            var spd = Math.Sqrt((vel.X * vel.X) + (vel.Z * vel.Z));
            if (spd > 0.35 && carts[i].FeetPosition.DistanceTo(player.Camera.Position) < 22.0)
            {
                var gain = Math.Clamp((float)(spd / 8.0) * 0.48f, 0.12f, 0.52f) * player.MasterVolume;
                var pitch = Math.Clamp(0.82f + ((float)spd * 0.04f), 0.80f, 1.25f);
                _audio.PlayPositional(SoundCue.MinecartRoll, carts[i].FeetPosition, gain, pitch);
                break;
            }
        }
    }

    private void StepAmbientMobAudio(ClientPlayerController player, float dt)
    {
        _ambientMobSoundTimer += dt;
        if (_ambientMobSoundTimer < 4.2 || _mobManager.Mobs.Count == 0) return;
        _ambientMobSoundTimer = 0.0;

        var idx = Math.Abs((int)(TimeCycle.TotalTicks % _mobManager.Mobs.Count));
        var mob = _mobManager.Mobs[idx];
        if (mob.FeetPosition.DistanceTo(player.Camera.Position) > 26.0) return;

        SoundCue? cue = mob.Type switch
        {
            MobType.Pig => SoundCue.MobPigSay,
            MobType.Sheep => SoundCue.MobSheepSay,
            MobType.Cow => SoundCue.MobCowSay,
            MobType.Chicken => SoundCue.MobChickenSay,
            MobType.Zombie => SoundCue.MobZombieSay,
            MobType.Skeleton => SoundCue.MobSkeletonSay,
            MobType.Spider => SoundCue.MobSpiderSay,
            _ => null,
        };
        if (cue is { } validCue)
        {
            _audio.PlayPositional(validCue, mob.FeetPosition, gain: 0.62f * player.MasterVolume);
        }
    }

    private (byte SkyLight, byte BlockLight, bool IsGrassTop, bool IsSolidGround, bool IsClearSpace) ProbeMobSpawnCell(BlockPos feetPos)
    {
        var belowState = _worldCache.GetBlockState(new BlockPos(feetPos.X, feetPos.Y - 1, feetPos.Z));
        var belowKey = _blocks.GetStateMetadata(belowState).BlockKey;
        var belowDef = _blocks.GetRequired(belowKey);
        var f0 = _blocks.GetRequired(_blocks.GetStateMetadata(_worldCache.GetBlockState(feetPos)).BlockKey);
        var f1 = _blocks.GetRequired(_blocks.GetStateMetadata(_worldCache.GetBlockState(new BlockPos(feetPos.X, feetPos.Y + 1, feetPos.Z))).BlockKey);
        var (sky, blk) = _worldCache.GetLight(feetPos);
        var clear = !f0.IsSolid && !f0.IsFluid && !f1.IsSolid && !f1.IsFluid;
        return (sky, blk, belowKey == BuiltInBlocks.GrassBlock, belowDef.IsSolid, clear);
    }
}
