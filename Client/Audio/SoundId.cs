namespace Client.Audio;

/// <summary>
/// Identifies every sound effect the client can play.
/// Grouped by category to match the audio system design (Task 9.1).
/// </summary>
public enum SoundId
{
    // ── Combat ────────────────────────────────────────────────────────────
    HitMelee = 0,
    HitMiss,
    HitSpell,

    // ── UI ────────────────────────────────────────────────────────────────
    UiClick,
    UiOpenContainer,
    UiEquip,

    // ── Ambient ───────────────────────────────────────────────────────────
    AmbientWater,
    AmbientWind,
    AmbientFire,

    // ── Creature ──────────────────────────────────────────────────────────
    CreatureGrowl,
}
