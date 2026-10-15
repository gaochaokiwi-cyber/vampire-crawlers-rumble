namespace VampireCrawlersRumble;

internal static class HapticWaveforms
{
    private const float Tau = 2f * MathF.PI;

    internal static void Pulse(PulseKind kind, float progress, out float low, out float high)
    {
        float x = Math.Clamp(progress, 0f, 1f);
        float tail = 1f - x;
        switch (kind)
        {
            case PulseKind.VillageMove:
                low = 0.10f * tail;
                high = 0.30f * tail;
                break;
            case PulseKind.VillageConfirm:
                low = 0.13f * tail;
                high = 0.34f * tail;
                break;
            case PulseKind.Wall:
                low = 1.00f * MathF.Pow(tail, 1.7f);
                high = 0.58f * MathF.Pow(tail, 2.2f);
                break;
            case PulseKind.RewardObject:
                low = 1.00f * MathF.Pow(tail, 1.5f);
                high = 0.67f * MathF.Pow(tail, 2f);
                break;
            case PulseKind.HardImpact:
                low = 0.66f * MathF.Pow(tail, 2.2f);
                high = 0.53f * MathF.Pow(tail, 3f);
                break;
            case PulseKind.SoftImpact:
                float softArc = Math.Max(0f, MathF.Sin(MathF.PI * x));
                low = 0.16f * softArc;
                high = 0.39f * softArc;
                break;
            case PulseKind.PlayerHit:
                low = 0.96f * MathF.Pow(tail, 0.8f);
                high = 0.74f * MathF.Pow(tail, 1.1f);
                break;
            case PulseKind.CoinTick:
                low = 0.05f * tail;
                high = 0.32f * MathF.Pow(tail, 2f);
                break;
            case PulseKind.CrystalTick:
                low = 0.03f * tail;
                high = 0.24f * Math.Max(0f, MathF.Sin(MathF.PI * x));
                break;
            case PulseKind.Test:
                low = 0.35f;
                high = 0.35f;
                break;
            default:
                low = high = 0f;
                break;
        }
    }

    internal static void Weapon(WeaponStyle style, float elapsed, float remaining, out float low, out float high)
    {
        float gain = Math.Min(1f, elapsed / 0.08f) * Math.Min(1f, remaining / 0.14f);
        if (style == WeaponStyle.Hard)
        {
            float grit = 0.5f + 0.5f * MathF.Sin(Tau * 7f * elapsed);
            low = gain * (0.13f + 0.055f * grit);
            high = gain * (0.06f + 0.10f * grit * grit);
        }
        else if (style == WeaponStyle.Soft)
        {
            float swell = 0.5f + 0.5f * MathF.Sin(Tau * 3.5f * elapsed);
            low = gain * (0.07f + 0.05f * swell);
            high = gain * (0.15f + 0.14f * swell);
        }
        else
        {
            float wave = 0.5f + 0.5f * MathF.Sin(Tau * 5f * elapsed);
            low = gain * (0.10f + 0.055f * wave);
            high = gain * (0.11f + 0.09f * wave);
        }
    }

    internal static void EnemySpell(float elapsed, float remaining, out float low, out float high)
    {
        float gain = Math.Min(1f, elapsed / 0.09f) * Math.Min(1f, remaining / 0.15f);
        float wave = 0.5f + 0.5f * MathF.Sin(Tau * 4.5f * elapsed);
        low = gain * (0.055f + 0.045f * wave);
        high = gain * (0.15f + 0.17f * wave);
    }

    internal static void CoinStream(float elapsed, float remaining, out float low, out float high)
    {
        float gain = Math.Min(1f, remaining / 0.12f);
        float beat = MathF.Pow(Math.Max(0f, MathF.Sin(Tau * 9f * elapsed)), 5f);
        low = gain * (0.035f + 0.075f * beat);
        high = gain * (0.10f + 0.23f * beat);
    }

    internal static void CrystalStream(float elapsed, float remaining, out float low, out float high)
    {
        float gain = Math.Min(1f, remaining / 0.13f);
        float shimmer = 0.5f + 0.5f * MathF.Sin(Tau * 6.5f * elapsed);
        low = gain * (0.025f + 0.025f * shimmer);
        high = gain * (0.13f + 0.19f * shimmer);
    }
}
