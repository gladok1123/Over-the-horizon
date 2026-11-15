using System.Numerics;

namespace OverHorizon.Rendering.Hud;

/// <summary>Active potion/food status effect badge displayed in the top-right HUD corner.</summary>
public readonly record struct HudStatusEffectVisual(
    string Label,
    int RemainingSeconds,
    Vector3 AccentColor);
