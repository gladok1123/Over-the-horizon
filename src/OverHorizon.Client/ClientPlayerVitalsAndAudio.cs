using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay;

namespace OverHorizon.Client;

/// <summary>
/// Manages underwater head detection, oxygen/drowning, Survival hunger &amp; regeneration, and material footstep audio
/// for <see cref="ClientPlayerController"/>.
/// </summary>
internal sealed class ClientPlayerVitalsAndAudio
{
    private const double FootstepStrideBlocks = 1.80;

    private double _stepDistanceAccumulator;
    private double _hungerDrainTimer;
    private double _regenTimer;
    private double _drownTimer;
    private double _swimSoundTimer;
    private int _stepPitchCounter;

    public void ResetTimers()
    {
        _stepDistanceAccumulator = 0.0;
        _hungerDrainTimer = 0.0;
        _regenTimer = 0.0;
        _drownTimer = 0.0;
        _swimSoundTimer = 0.0;
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
            audio.PlayPositional(SoundCue.UnderwaterEnter, eyePos, gain: 0.70f * masterVolume, pitch: 0.95f);
        }
        else if (wasUnderwater && !isUnderwater)
        {
            audio.PlayPositional(SoundCue.WaterSplash, eyePos, gain: 0.50f * masterVolume, pitch: 1.12f);
        }

        if (isSwimming)
        {
            _swimSoundTimer += dt;
            if (_swimSoundTimer >= 0.68)
            {
                _swimSoundTimer = 0.0;
                var cue = isUnderwater ? SoundCue.UnderwaterEnter : SoundCue.WaterSplash;
                audio.PlayPositional(cue, feetPos, gain: 0.35f * masterVolume, pitch: 0.92f);
            }
        }

        return isUnderwater;
    }

    public void StepSurvivalVitals(
        ClientPlayerController player,
        double maxAirSeconds,
        double dt,
        bool movingOnGround,
        bool sprinting,
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

        if (movingOnGround)
        {
            _hungerDrainTimer += dt * (sprinting ? 2.2 : 0.65);
            if (_hungerDrainTimer >= 22.0)
            {
                _hungerDrainTimer = 0.0;
                player.SetHungerInternal(Math.Max(0, player.Hunger - 1));
            }
        }

        if (!player.IsDead && player.Health < 20.0f && player.Hunger >= 18)
        {
            _regenTimer += dt;
            if (_regenTimer >= 2.5)
            {
                _regenTimer = 0.0;
                player.SetHealthInternal(MathF.Min(20.0f, player.Health + 1.0f));
            }
        }
        else
        {
            _regenTimer = 0.0;
        }
    }

    public void UpdateFootstepAudio(
        ClientPlayerController player,
        ClientWorldCache world,
        IBlockRegistry blocks,
        IAudioSubsystem audio,
        double horizontalDist)
    {
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
        var belowPos = new BlockPos(
            (int)Math.Floor(player.FeetPosition.X),
            (int)Math.Floor(player.FeetPosition.Y - 0.2),
            (int)Math.Floor(player.FeetPosition.Z));

        var groundKey = blocks.GetStateMetadata(world.GetBlockState(belowPos)).BlockKey.Path;
        var cue = groundKey switch
        {
            "grass_block" or "dirt" or "farmland" or "hay_block" or "sand" or "gravel" or "oak_leaves" or "birch_leaves" or "snow_block" or "crimson_wool" or "white_wool" or "blue_wool" => SoundCue.FootstepGrass,
            "oak_log" or "birch_log" or "oak_planks" or "birch_planks" or "oak_fence" or "crafting_table" or "chest" or "barrel" or "bookshelf" or "ladder" => SoundCue.FootstepWood,
            _ => SoundCue.FootstepStone,
        };

        var pitchJitter = 0.93f + (((_stepPitchCounter++ * 37) % 15) * 0.01f);
        audio.PlayPositional(cue, player.FeetPosition, gain: 0.50f * player.MasterVolume, pitch: pitchJitter);
    }
}
