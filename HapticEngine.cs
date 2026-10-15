using Nosebleed.Pancake.GameCommands;
using Nosebleed.Pancake.GameConfig;
using Nosebleed.Pancake.Models;
using Nosebleed.Pancake.View;
using Nosebleed.Pancake.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VampireCrawlersRumble;

internal enum PulseKind
{
    VillageMove, VillageConfirm, Wall, RewardObject, HardImpact, SoftImpact,
    PlayerHit, CoinTick, CrystalTick, Test
}

internal enum WeaponStyle { Hard, Soft, Neutral }

internal readonly record struct HapticPulse(PulseKind Kind, float Start, float Duration);
internal readonly record struct WeaponTrack(WeaponStyle Style, float Start, float Expires);
internal readonly record struct EnemySpellTrack(float Start, float Expires);
internal readonly record struct EnemyVisualHitCue(EnemyView View, float At, bool Fallback);

internal sealed class EnemyLungeTrack
{
    internal readonly EnemyView View;
    internal readonly Transform MoveTransform;
    internal readonly Vector3 StartPosition;
    internal readonly LungeImpactDetector Detector = new();
    internal readonly float Expires;

    internal EnemyLungeTrack(EnemyView view, Transform moveTransform, float expires)
    {
        View = view;
        MoveTransform = moveTransform;
        StartPosition = moveTransform.localPosition;
        Expires = expires;
    }
}

public sealed class RumbleRunner : MonoBehaviour
{
    private readonly List<HapticPulse> _pulses = new();
    private readonly Dictionary<int, WeaponTrack> _weapons = new();
    private readonly List<EnemySpellTrack> _enemySpells = new();
    private readonly Dictionary<int, EnemyLungeTrack> _enemyLunges = new();
    private readonly Dictionary<int, float> _lastVisualHitByEnemy = new();
    private readonly List<EnemyVisualHitCue> _enemyVisualCues = new();
    private Gamepad _pad;
    private float _lastVillageMove = -10f;
    private float _lastVillageConfirm = -10f;
    private float _lastWall = -10f;
    private float _lastRewardBump = -10f;
    private float _lastWeaponImpact = -10f;
    private float _lastCoinTick = -10f;
    private float _lastCrystalTick = -10f;
    private float _coinStart;
    private float _coinUntil;
    private float _crystalStart;
    private float _crystalUntil;
    private float _lastPlayerHitStart = -10f;
    private float _lastFallbackCue = -10f;
    private float _sentLow;
    private float _sentHigh;
    private float _nextMotorUpdate;
    private bool _testDone;
    private bool _motorFailed;

    private static bool Ready => Plugin.Instance?.Enabled.Value == true &&
        Application.isFocused && Gamepad.current != null;

    internal void VillageSelection()
    {
        if (!Ready || !Plugin.Instance.VillageNavigation.Value) return;
        float now = Time.realtimeSinceStartup;
        if (now - _lastVillageMove < 0.08f) return;
        _lastVillageMove = now;
        Add(PulseKind.VillageMove, 0.065f);
        DebugEvent("village selection");
    }

    internal void VillageConfirm()
    {
        if (!Ready || !Plugin.Instance.VillageNavigation.Value) return;
        float now = Time.realtimeSinceStartup;
        if (now - _lastVillageConfirm < 0.12f) return;
        _lastVillageConfirm = now;
        Add(PulseKind.VillageConfirm, 0.09f);
        DebugEvent("village confirm");
    }

    internal void WallBump()
    {
        if (!Ready || !Plugin.Instance.DungeonBumps.Value) return;
        float now = Time.realtimeSinceStartup;
        if (now - _lastWall < 0.16f || now - _lastRewardBump < 0.12f) return;
        _lastWall = now;
        Add(PulseKind.Wall, 0.13f);
        DebugEvent("wall bump");
    }

    internal void RewardBump()
    {
        if (!Ready || !Plugin.Instance.DungeonBumps.Value) return;
        float now = Time.realtimeSinceStartup;
        if (now - _lastRewardBump < 0.16f) return;
        _lastRewardBump = now;
        Add(PulseKind.RewardObject, 0.18f);
        DebugEvent("reward object bump");
    }

    internal void CoinSpawned()
    {
        if (!Ready || !Plugin.Instance.Rewards.Value) return;
        float now = Time.realtimeSinceStartup;
        if (now >= _coinUntil) _coinStart = now;
        _coinUntil = Math.Max(_coinUntil, now + 0.30f);
        DebugEvent("coin spawned");
    }

    internal void CoinArrived()
    {
        if (!Ready || !Plugin.Instance.Rewards.Value) return;
        float now = Time.realtimeSinceStartup;
        if (now - _lastCoinTick < 0.035f) return;
        _lastCoinTick = now;
        _coinUntil = Math.Max(_coinUntil, now + 0.16f);
        Add(PulseKind.CoinTick, 0.075f);
    }

    internal void CrystalSpawned()
    {
        if (!Ready || !Plugin.Instance.Rewards.Value) return;
        float now = Time.realtimeSinceStartup;
        if (now >= _crystalUntil) _crystalStart = now;
        _crystalUntil = Math.Max(_crystalUntil, now + 0.34f);
        DebugEvent("experience crystal spawned");
    }

    internal void CrystalArrived()
    {
        if (!Ready || !Plugin.Instance.Rewards.Value) return;
        float now = Time.realtimeSinceStartup;
        if (now - _lastCrystalTick < 0.035f) return;
        _lastCrystalTick = now;
        _crystalUntil = Math.Max(_crystalUntil, now + 0.18f);
        Add(PulseKind.CrystalTick, 0.09f);
    }

    internal void WeaponStarted(Weapon weapon)
    {
        if (!Ready || !Plugin.Instance.CardEffects.Value || weapon == null) return;
        float now = Time.realtimeSinceStartup;
        int id = weapon.GetInstanceID();
        if (_weapons.TryGetValue(id, out var existing) && now - existing.Start < 0.10f) return;
        WeaponStyle style = Classify(weapon);
        float duration = 1.65f;
        try
        {
            var animated = weapon.GetComponent<AnimatedWeapon>();
            if (animated != null && animated._animationClip != null)
                duration = Math.Clamp(animated._animationClip.length + 0.12f, 0.25f, 4f);
        }
        catch { /* Other weapon implementations use the OnDisable boundary. */ }
        _weapons[id] = new WeaponTrack(style, now, now + duration);
        DebugEvent($"weapon started: {weapon.CardConfig?.Name ?? weapon.name} ({style})");
    }

    internal void WeaponStopped(Weapon weapon)
    {
        if (weapon == null) return;
        _weapons.Remove(weapon.GetInstanceID());
    }

    internal void WeaponImpact(Weapon weapon)
    {
        if (!Ready || !Plugin.Instance.CardEffects.Value || weapon == null) return;
        float now = Time.realtimeSinceStartup;
        if (now - _lastWeaponImpact < 0.055f) return;
        _lastWeaponImpact = now;
        WeaponStyle style = Classify(weapon);
        Add(style == WeaponStyle.Hard ? PulseKind.HardImpact : PulseKind.SoftImpact,
            style == WeaponStyle.Hard ? 0.11f : 0.17f);
        int id = weapon.GetInstanceID();
        if (_weapons.TryGetValue(id, out var track))
            _weapons[id] = track with { Expires = Math.Max(track.Expires, now + 0.16f) };
        DebugEvent($"weapon impact: {style}");
    }

    internal void EnemyAttackVisualStarted(EnemyActionDamage action, EnemyModel enemy, float durationMultiplier)
    {
        if (!Ready || action == null) return;
        CancelVisualFallbacks(enemy?.EnemyView);
        var clip = action._bespokeAnimationClip;
        string animationName = clip?.name ?? "";
        if (IsSpell(animationName))
        {
            if (!Plugin.Instance.EnemyAttacks.Value) return;
            float duration = clip != null ? clip.length : action._actionAnimationOverride?.Duration ?? 0.8f;
            StartEnemySpell(duration * Math.Max(0.1f, durationMultiplier), "enemy spell animation");
            return;
        }
        var view = enemy?.EnemyView;
        BeginEnemyLunge(view, 2.5f * Math.Max(0.1f, durationMultiplier));
        ScheduleVisualFallback(view, GetAttackInTime(action) * Math.Max(0.1f, durationMultiplier));
    }

    internal void EnemyMultiAttackVisualStarted(EnemyActionMultiDamage action, EnemyModel enemy, float durationMultiplier)
    {
        if (!Ready || action == null) return;
        CancelVisualFallbacks(enemy?.EnemyView);
        if (IsSpell(action._attackVFXPrefab?.name ?? "")) return;
        var view = enemy?.EnemyView;
        BeginEnemyLunge(view,
            2.5f * Math.Max(1, action._numberOfAttacks) * Math.Max(0.1f, durationMultiplier));
        float first = 0.25f;
        float interval = 0.3f;
        try
        {
            var timing = GlobalConfig.Instance.enemySettingsConfig;
            if (timing.multiAttackInTime > 0f) first = timing.multiAttackInTime;
            float configuredInterval = timing.multiAttackHitTime + timing.multiAttackOutTime;
            if (configuredInterval > 0f) interval = configuredInterval;
        }
        catch { /* Use bounded timing if the config is not ready. */ }
        for (int hit = 0; hit < Math.Clamp(action._numberOfAttacks, 1, 8); hit++)
            ScheduleVisualFallback(view, (first + hit * interval) * Math.Max(0.1f, durationMultiplier));
    }

    internal void EnemyActionExecuting(EnemyAction action, EnemyModel enemy, float durationMultiplier)
    {
        if (action == null) return;
        var damage = action.TryCast<EnemyActionDamage>();
        var multi = action.TryCast<EnemyActionMultiDamage>();
        DebugEvent($"enemy action execute: damage={damage != null}, multi={multi != null}, view={enemy?.EnemyView != null}, ready={Ready}");
        if (!Ready) return;
        if (damage != null) EnemyAttackVisualStarted(damage, enemy, durationMultiplier);
        else if (multi != null) EnemyMultiAttackVisualStarted(multi, enemy, durationMultiplier);
    }

    internal void EnemyActionPerformedProbe(EnemyAction action)
    {
        if (action == null) return;
        DebugEvent($"enemy performed action: damage={action.TryCast<EnemyActionDamage>() != null}, " +
            $"multi={action.TryCast<EnemyActionMultiDamage>() != null}");
    }

    internal void EnemyTurnProbe() => DebugEvent("enemy turn execution entered");

    internal void DamageProbe(int amount) => DebugEvent($"diagnostic damage settlement: {amount}");

    internal void EnemyAnimationProbe(EnemyView view, AnimationClip clip) =>
        DebugEvent($"enemy custom animation: {clip?.name ?? "<null>"}, view={view != null}");

    internal void EnemyVisualHitCueScheduled(EnemyModel enemy, float animationWait)
    {
        if (!Ready || !Plugin.Instance.PlayerHits.Value || enemy == null) return;
        var view = enemy.EnemyView;
        if (view == null) return;
        _enemyLunges.Remove(view.GetInstanceID());
        _enemyVisualCues.RemoveAll(cue => cue.Fallback && cue.View != null &&
            cue.View.GetInstanceID() == view.GetInstanceID());
        if (_enemyVisualCues.Count >= 24) _enemyVisualCues.RemoveAt(0);
        _enemyVisualCues.Add(new EnemyVisualHitCue(view,
            HapticScheduling.VisualCueTime(Time.realtimeSinceStartup, animationWait), false));
        DebugEvent($"enemy visual hit cue scheduled: {animationWait:0.000}s");
    }

    private void ScheduleVisualFallback(EnemyView view, float animationWait)
    {
        if (!Plugin.Instance.PlayerHits.Value || view == null) return;
        if (_enemyVisualCues.Count >= 24) _enemyVisualCues.RemoveAt(0);
        float now = Time.realtimeSinceStartup;
        float at = HapticScheduling.NextFallbackCue(now, animationWait, _lastFallbackCue);
        _lastFallbackCue = at;
        _enemyVisualCues.Add(new EnemyVisualHitCue(view, at, true));
        DebugEvent($"enemy animation fallback scheduled: {animationWait:0.000}s, cue in {at - now:0.000}s");
    }

    private static float GetAttackInTime(EnemyActionDamage action)
    {
        if (action._bespokeAnimationActionWait > 0f) return action._bespokeAnimationActionWait;
        try
        {
            float configured = GlobalConfig.Instance.enemySettingsConfig.attackInTime;
            if (configured > 0f) return configured;
        }
        catch { /* Fall back if the config singleton is not ready. */ }
        return 0.25f;
    }

    internal void EnemyAttackVfxStarted(EnemyActionMultiDamage action, EnemyView enemyView)
    {
        if (!Ready || action == null) return;
        var prefab = action._attackVFXPrefab;
        if (prefab == null || !IsSpell(prefab.name))
        {
            CancelNextVisualCue(enemyView);
            VisualImpact(enemyView, "enemy attack VFX impact");
            return;
        }
        if (!Plugin.Instance.EnemyAttacks.Value) return;
        float duration = 0.8f;
        try
        {
            var particles = prefab.GetComponentInChildren<ParticleSystem>();
            if (particles != null) duration = particles.main.duration;
        }
        catch { /* A visual prefab need not have a particle system. */ }
        StartEnemySpell(duration, "enemy spell VFX");
    }

    private void BeginEnemyLunge(EnemyView view, float timeout)
    {
        if (!Plugin.Instance.PlayerHits.Value || view == null) return;
        var move = view.MoveTransform;
        if (move == null) return;
        _enemyLunges[view.GetInstanceID()] =
            new EnemyLungeTrack(view, move, Time.realtimeSinceStartup + Math.Clamp(timeout, 1f, 8f));
        DebugEvent("enemy lunge animation started");
    }

    private void StartEnemySpell(float duration, string source)
    {
        float now = Time.realtimeSinceStartup;
        if (_enemySpells.Count >= 16) _enemySpells.RemoveAt(0);
        _enemySpells.Add(new EnemySpellTrack(now, now + Math.Clamp(duration, 0.25f, 3f)));
        DebugEvent(source);
    }

    private void VisualImpact(EnemyView view, string source)
    {
        if (!Ready || !Plugin.Instance.PlayerHits.Value) return;
        float now = Time.realtimeSinceStartup;
        if (view != null)
        {
            int id = view.GetInstanceID();
            if (_lastVisualHitByEnemy.TryGetValue(id, out float last) && now - last < 0.12f) return;
            _lastVisualHitByEnemy[id] = now;
        }
        // Leave a motor-off interval so later enemies remain perceptible.
        float start = HapticScheduling.NextImpactStart(now, _lastPlayerHitStart);
        _lastPlayerHitStart = start;
        Add(PulseKind.PlayerHit, 0.070f, start - now);
        DebugEvent($"{source}; motor in {start - now:0.000}s");
    }

    private void CancelNextVisualCue(EnemyView view)
    {
        if (view == null) return;
        int id = view.GetInstanceID();
        int next = -1;
        float earliest = float.MaxValue;
        for (int i = 0; i < _enemyVisualCues.Count; i++)
        {
            var cue = _enemyVisualCues[i];
            if (cue.View == null || cue.View.GetInstanceID() != id || cue.At >= earliest) continue;
            next = i;
            earliest = cue.At;
        }
        if (next >= 0) _enemyVisualCues.RemoveAt(next);
    }

    private void CancelVisualFallbacks(EnemyView view)
    {
        if (view == null) return;
        int id = view.GetInstanceID();
        _enemyVisualCues.RemoveAll(cue => cue.Fallback && cue.View != null &&
            cue.View.GetInstanceID() == id);
    }

    internal void EndEncounter()
    {
        _weapons.Clear();
        _enemySpells.Clear();
        _enemyLunges.Clear();
        _lastVisualHitByEnemy.Clear();
        _enemyVisualCues.Clear();
        _lastFallbackCue = -10f;
        _lastPlayerHitStart = -10f;
    }

    private void Add(PulseKind kind, float duration, float delay = 0f)
    {
        if (_pulses.Count >= 48) _pulses.RemoveAt(0);
        _pulses.Add(new HapticPulse(kind, Time.realtimeSinceStartup + delay, duration));
    }

    private void DebugEvent(string message)
    {
        if (Plugin.Instance?.DebugEvents.Value == true)
            Plugin.Instance.Log.LogInfo($"Haptic event: {message}");
    }

    private static WeaponStyle Classify(Weapon weapon)
    {
        string name = ((weapon.CardConfig?.Name ?? "") + " " + (weapon.CardConfig?.name ?? "") +
            " " + weapon.name + " " + weapon.GetType().Name).ToLowerInvariant();
        foreach (string token in HardNames)
            if (name.Contains(token)) return WeaponStyle.Hard;
        foreach (string token in SoftNames)
            if (name.Contains(token)) return WeaponStyle.Soft;
        return WeaponStyle.Neutral;
    }

    private static readonly string[] HardNames =
    {
        "whip", "knife", "axe", "sword", "cross", "bone", "drill", "pinion",
        "gun", "pistol", "bullet", "carrello", "lancet", "bible"
    };

    private static readonly string[] SoftNames =
    {
        "magic", "wand", "fire", "water", "mana", "lightning", "thunder", "garlic",
        "laurel", "moon", "pentagram", "song", "spell", "holy", "rune", "santa", "phiera"
    };

    private static bool IsSpell(string name)
    {
        string lower = name.ToLowerInvariant();
        foreach (string token in SpellNames)
            if (lower.Contains(token)) return true;
        return false;
    }

    private static readonly string[] SpellNames =
    {
        "spell", "magic", "cast", "wizard", "sorcer", "fireball", "flame",
        "lightning", "thunder", "ice", "frost", "water", "meteor", "beam",
        "orb", "arcane", "curse", "poison", "laser", "projectile"
    };

    private void Update()
    {
        var plugin = Plugin.Instance;
        var current = Gamepad.current;
        if (plugin == null || !plugin.Enabled.Value || !Application.isFocused || current == null)
        {
            StopMotor();
            return;
        }
        if (_pad != current)
        {
            StopMotor();
            _pad = current;
            _motorFailed = false;
            plugin.Log.LogInfo($"Gamepad detected: {current.displayName}");
        }
        if (plugin.TestOnStart.Value && !_testDone)
        {
            _testDone = true;
            Add(PulseKind.Test, 0.25f);
            plugin.Log.LogInfo("Sent controller test pulse.");
        }

        float now = Time.realtimeSinceStartup;
        float low = 0f;
        float high = 0f;
        for (int i = _pulses.Count - 1; i >= 0; i--)
        {
            var pulse = _pulses[i];
            if (now >= pulse.Start + pulse.Duration)
            {
                _pulses.RemoveAt(i);
                continue;
            }
            if (now < pulse.Start) continue;
            HapticWaveforms.Pulse(pulse.Kind, (now - pulse.Start) / pulse.Duration, out float l, out float h);
            low = Math.Max(low, l);
            high = Math.Max(high, h);
        }

        foreach (int id in _weapons.Keys.ToArray())
        {
            var track = _weapons[id];
            if (now >= track.Expires)
            {
                _weapons.Remove(id);
                continue;
            }
            HapticWaveforms.Weapon(track.Style, now - track.Start, track.Expires - now, out float l, out float h);
            low = Math.Max(low, l);
            high = Math.Max(high, h);
        }

        foreach (int id in _enemyLunges.Keys.ToArray())
        {
            var lunge = _enemyLunges[id];
            if (now >= lunge.Expires)
            {
                _enemyLunges.Remove(id);
                continue;
            }
            try
            {
                float distance = Vector3.Distance(lunge.MoveTransform.localPosition, lunge.StartPosition);
                if (lunge.Detector.Sample(distance, now))
                {
                    CancelNextVisualCue(lunge.View);
                    VisualImpact(lunge.View, "enemy lunge visual impact");
                }
            }
            catch { _enemyLunges.Remove(id); }
        }

        for (int i = _enemyVisualCues.Count - 1; i >= 0; i--)
        {
            var cue = _enemyVisualCues[i];
            if (now < cue.At) continue;
            _enemyVisualCues.RemoveAt(i);
            if (cue.View != null) _enemyLunges.Remove(cue.View.GetInstanceID());
            VisualImpact(cue.View, "enemy animation hit cue");
        }

        for (int i = _enemySpells.Count - 1; i >= 0; i--)
        {
            var spell = _enemySpells[i];
            if (now >= spell.Expires)
            {
                _enemySpells.RemoveAt(i);
                continue;
            }
            HapticWaveforms.EnemySpell(now - spell.Start, spell.Expires - now, out float l, out float h);
            low = Math.Max(low, l);
            high = Math.Max(high, h);
        }
        if (now < _coinUntil)
        {
            HapticWaveforms.CoinStream(now - _coinStart, _coinUntil - now, out float l, out float h);
            low = Math.Max(low, l);
            high = Math.Max(high, h);
        }
        if (now < _crystalUntil)
        {
            HapticWaveforms.CrystalStream(now - _crystalStart, _crystalUntil - now, out float l, out float h);
            low = Math.Max(low, l);
            high = Math.Max(high, h);
        }

        float strength = Math.Clamp(plugin.Strength.Value, 0f, 1f);
        SendMotor(current, Math.Clamp(low * strength, 0f, 1f), Math.Clamp(high * strength, 0f, 1f), now);
    }

    private void SendMotor(Gamepad pad, float low, float high, float now)
    {
        if (_motorFailed) return;
        bool changed = Math.Abs(low - _sentLow) > 0.018f || Math.Abs(high - _sentHigh) > 0.018f;
        bool stopping = low < 0.001f && high < 0.001f && (_sentLow > 0f || _sentHigh > 0f);
        if (!stopping && (!changed || now < _nextMotorUpdate)) return;
        try
        {
            pad.SetMotorSpeeds(low, high);
            _sentLow = low;
            _sentHigh = high;
            _nextMotorUpdate = now + 1f / 30f;
        }
        catch (Exception ex)
        {
            _motorFailed = true;
            Plugin.Instance?.Log.LogError($"Gamepad haptics failed: {ex}");
        }
    }

    private void StopMotor()
    {
        if (_pad != null && (_sentLow > 0f || _sentHigh > 0f))
        {
            try { _pad.SetMotorSpeeds(0f, 0f); }
            catch (Exception ex) { Plugin.Instance?.Log.LogWarning($"Could not stop motor: {ex.Message}"); }
        }
        _sentLow = 0f;
        _sentHigh = 0f;
        _nextMotorUpdate = 0f;
    }

    private void OnDisable()
    {
        StopMotor();
        _weapons.Clear();
        _enemySpells.Clear();
        _enemyLunges.Clear();
        _lastVisualHitByEnemy.Clear();
        _enemyVisualCues.Clear();
        _lastFallbackCue = -10f;
        _lastPlayerHitStart = -10f;
        _pulses.Clear();
    }

    private void OnDestroy() => StopMotor();
}
