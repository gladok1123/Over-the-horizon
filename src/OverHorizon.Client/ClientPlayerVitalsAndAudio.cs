using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;

namespace OverHorizon.Client;

/// <summary>
/// Manages underwater head detection, reworked water &amp; swim audio, oxygen/drowning, Survival hunger/saturation/exhaustion,
/// fire hazard damage, and material-specific footstep/ladder audio for <see cref="ClientPlayerController"/>.
/// </summary>
internal sealed class ClientPlayerVitalsAndAudio
{
    private const double FootstepStrideBlocks = 1.80;

    private double _stepDistanceAccumulator;
    private double _ladderStepAccumulator;
    private double _drownTimer;
    private double _fireTickTimer;
    private double _swimSoundTimer;
    private double _underwaterAmbienceTimer;
    private int _stepPitchCounter;

    public void ResetTimers()
    {
        _stepDistanceAccumulator = 0.0;
        _ladderStepAccumulator = 0.0;
        _drownTimer = 0.0;
        _fireTickTimer = 0.0;
        _swimSoundTimer = 0.0;
        _underwaterAmbienceTimer = 0.0;
    }

    public bool UpdateUnderwaterState(
        ClientWorldCache world,
        IBlockRegistry blocks,
        IAudioSubsystem audio,
        Vec3d eyePos,
        Vec3d feetPos,
        bool wasUnderwater,
        float masterVolume,
        double dt,
        bool isSwimming)
    {
        var eyeKey = blocks.GetStateMetadata(world.GetBlockState(eyePos.ToBlockPos())).BlockKey;
        var isUnderwater = blocks.GetRequired(eyeKey).IsFluid;

        if (!wasUnderwater && isUnderwater)
        {
            audio.PlayPositional(SoundCue.UnderwaterEnter, eyePos, gain: 0.58f * masterVolume, pitch: 1.0f);
            _underwaterAmbienceTimer = 1.5;
        }
        else if (wasUnderwater && !isUnderwater)
        {
            audio.PlayPositional(SoundCue.UnderwaterExit, eyePos, gain: 0.52f * masterVolume, pitch: 1.04f);
            _underwaterAmbienceTimer = 0.0;
        }

        if (isUnderwater)
        {
            _underwaterAmbienceTimer += dt;
            if (_underwaterAmbienceTimer >= 5.2)
            {
                _underwaterAmbienceTimer = 0.0;
                audio.PlayPositional(SoundCue.UnderwaterAmbience, eyePos, gain: 0.24f * masterVolume, pitch: 1.0f);
            }
        }

        if (isSwimming)
        {
            _swimSoundTimer += dt;
            if (_swimSoundTimer >= 0.62)
            {
                _swimSoundTimer = 0.0;
                var pitch = 0.94f + (((_stepPitchCounter++ * 29) % 13) * 0.01f);
                audio.PlayPositional(SoundCue.WaterSwim, feetPos, gain: 0.42f * masterVolume, pitch: pitch);
            }
        }
        else
        {
            _swimSoundTimer = Math.Min(_swimSoundTimer, 0.45);
        }

        return isUnderwater;
    }

    public void StepSurvivalVitals(
        ClientPlayerController player,
        ResourceKey feetBlockKey,
        double maxAirSeconds,
        double dt,
        double horizontalMoved,
        bool sprinting,
        bool inFluid,
        IAudioSubsystem audio)
    {
        if (player.IsHeadUnderwater && player.Mode == GameMode.Survival)
        {
            player.SetAirSupplyInternal(Math.Max(0.0, player.AirSupplySeconds - dt));
            if (player.AirSupplySeconds <= 0.0)
            {
                _drownTimer += dt;
                if (_drownTimer >= 1.0)
                {
                    _drownTimer -= 1.0;
                    _ = player.ApplyDamage(2.0f, audio, bypassArmor: true);
                }
            }
        }
        else
        {
            player.SetAirSupplyInternal(Math.Min(maxAirSeconds, player.AirSupplySeconds + (dt * 3.5)));
            _drownTimer = 0.0;
        }

        if (player.Mode != GameMode.Survival)
        {
            return;
        }

        if (feetBlockKey == BuiltInBlocks.Fire)
        {
            _fireTickTimer += dt;
            if (_fireTickTimer >= 0.65)
            {
                _fireTickTimer -= 0.65;
                _ = player.ApplyDamage(1.0f, audio, bypassArmor: false, customCue: SoundCue.FireHurt);
            }
        }
        else
        {
            _fireTickTimer = 0.0;
        }

        if (horizontalMoved > 1e-4 && !player.IsFlying)
        {
            if (inFluid)
            {
                player.HungerModel.AddExhaustion((float)(horizontalMoved * 0.015));
            }
            else if (player.OnGround)
            {
                player.HungerModel.AddExhaustion((float)(horizontalMoved * (sprinting ? 0.10 : 0.008)));
            }
        }

        var (heal, starveDmg) = player.HungerModel.Step(dt, player.Health, maxHealth: 20.0f);
        if (heal > 0.0f)
        {
            player.SetHealthInternal(MathF.Min(20.0f, player.Health + heal));
        }

        if (starveDmg > 0.0f)
        {
            _ = player.ApplyDamage(starveDmg, audio, bypassArmor: true);
        }
    }

    public void UpdateFootstepAudio(
        ClientPlayerController player,
        ClientWorldCache world,
        IBlockRegistry blocks,
        IAudioSubsystem audio,
        double horizontalDist,
        double verticalDist,
        bool onLadder)
    {
        if (onLadder && !player.IsFlying && (Math.Abs(verticalDist) > 1e-4 || horizontalDist > 1e-4))
        {
            _ladderStepAccumulator += Math.Abs(verticalDist) + horizontalDist;
            if (_ladderStepAccumulator >= 1.15)
            {
                _ladderStepAccumulator -= 1.15;
                var pitch = 0.95f + (((_stepPitchCounter++ * 31) % 11) * 0.01f);
                audio.PlayPositional(SoundCue.FootstepLadder, player.FeetPosition, gain: 0.44f * player.MasterVolume, pitch: pitch);
            }

            return;
        }

        if (!player.OnGround || player.IsFlying || player.InFluid || horizontalDist <= 1e-4)
        {
            return;
        }

        _stepDistanceAccumulator += horizontalDist;
        if (_stepDistanceAccumulator < FootstepStrideBlocks)
        {
            return;
        }

        _stepDistanceAccumulator -= FootstepStrideBlocks;
        var groundKey = SampleStandingSurfaceBlockPath(player.FeetPosition, world, blocks);
        if (groundKey.Length == 0 || groundKey is "air" or "water" or "fire")
        {
            return;
        }

        var cue = ResolveFootstepCue(groundKey);
        var pitchJitter = 0.93f + (((_stepPitchCounter++ * 37) % 15) * 0.01f);
        audio.PlayPositional(cue, player.FeetPosition, gain: 0.48f * player.MasterVolume, pitch: pitchJitter);
    }

    private static string SampleStandingSurfaceBlockPath(Vec3d feet, ClientWorldCache world, IBlockRegistry blocks)
    {
        // 1. Check feet voxel itself first (Rails, Powered Rails, Red Bed slabs, Pressure Plates)!
        var atFeet = blocks.GetStateMetadata(world.GetBlockState(feet.ToBlockPos())).BlockKey.Path;
        if (atFeet is "rail" or "powered_rail" or "red_bed" or "stone_pressure_plate")
        {
            return atFeet;
        }

        // 2. Check center + 4 footprint corners at Y - 0.2 so walking along block edges NEVER samples Air -> Stone!
        var sampleY = (int)Math.Floor(feet.Y - 0.2);
        ReadOnlySpan<(double Ox, double Oz)> offsets =
        [
            (0.0, 0.0), (-0.28, -0.28), (0.28, -0.28), (-0.28, 0.28), (0.28, 0.28),
        ];

        for (var i = 0; i < offsets.Length; i++)
        {
            var bp = new BlockPos((int)Math.Floor(feet.X + offsets[i].Ox), sampleY, (int)Math.Floor(feet.Z + offsets[i].Oz));
            var state = world.GetBlockState(bp);
            if (state.IsAir) continue;
            var path = blocks.GetStateMetadata(state).BlockKey.Path;
            if (path is not ("air" or "water" or "torch" or "redstone_torch" or "redstone_wire"))
            {
                return path;
            }
        }

        return string.Empty;
    }

    public static SoundCue ResolveFootstepCue(string blockPath) => blockPath switch
    {
        "grass_block" or "dirt" or "farmland" or "hay_block" or "oak_leaves" or "birch_leaves" => SoundCue.FootstepGrass,
        "sand" => SoundCue.FootstepSand,
        "gravel" => SoundCue.FootstepGravel,
        "snow_block" => SoundCue.FootstepSnow,
        "iron_block" or "gold_block" or "copper_block" or "diamond_block" or "emerald_block" or "lantern" or "rail" or "powered_rail" => SoundCue.FootstepMetal,
        "crimson_wool" or "white_wool" or "blue_wool" or "red_bed" or "tnt" or "cobweb" => SoundCue.FootstepCloth,
        "ladder" => SoundCue.FootstepLadder,
        "oak_log" or "birch_log" or "oak_planks" or "birch_planks" or "oak_fence" or "crafting_table" or "chest" or "barrel" or "bookshelf" or "piston" or "sticky_piston" or "piston_head" => SoundCue.FootstepWood,
        _ => SoundCue.FootstepStone,
    };
}
