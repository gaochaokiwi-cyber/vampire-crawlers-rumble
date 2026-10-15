namespace VampireCrawlersRumble;

internal static class HapticScheduling
{
    internal const float EnemyImpactSpacing = 0.115f;

    internal static float NextImpactStart(float now, float previousStart) =>
        Math.Max(now, previousStart + EnemyImpactSpacing);

    internal static float NextFallbackCue(float now, float animationWait, float previousCue) =>
        NextImpactStart(VisualCueTime(now, animationWait), previousCue);

    internal static float VisualCueTime(float now, float animationWait) =>
        now + Math.Clamp(animationWait, 0f, 2f);
}

internal sealed class LungeImpactDetector
{
    private float _peak;
    private float _lastRise;
    private bool _fired;

    internal bool Sample(float distanceFromStart, float now)
    {
        float distance = Math.Max(0f, distanceFromStart);
        if (_fired)
        {
            if (distance <= Math.Max(0.03f, _peak * 0.4f))
            {
                _peak = 0f;
                _fired = false;
            }
            return false;
        }

        if (distance > _peak + 0.004f)
        {
            _peak = distance;
            _lastRise = now;
            return false;
        }

        if (_peak < 0.06f) return false;
        if (_peak - distance < 0.008f && now - _lastRise < 0.035f) return false;
        _fired = true;
        return true;
    }
}
