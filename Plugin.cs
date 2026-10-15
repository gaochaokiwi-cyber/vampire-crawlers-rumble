using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Nosebleed.Pancake.DungeonEnvironment;
using Nosebleed.Pancake.GameCommands;
using Nosebleed.Pancake.Models;
using Nosebleed.Pancake.View;
using Nosebleed.Pancake.Weapons;
using UnityEngine;

namespace VampireCrawlersRumble;

[BepInPlugin(Id, "Vampire Crawlers Rumble", Version)]
public sealed class Plugin : BasePlugin
{
    public const string Id = "com.gamestar.vampirecrawlers.rumble";
    public const string Version = "0.2.5";
    internal static Plugin Instance { get; private set; }
    internal static RumbleRunner Runner { get; private set; }

    internal ConfigEntry<bool> Enabled;
    internal ConfigEntry<float> Strength;
    internal ConfigEntry<bool> VillageNavigation;
    internal ConfigEntry<bool> DungeonBumps;
    internal ConfigEntry<bool> CardEffects;
    internal ConfigEntry<bool> Rewards;
    internal ConfigEntry<bool> EnemyAttacks;
    internal ConfigEntry<bool> PlayerHits;
    internal ConfigEntry<bool> TestOnStart;
    internal ConfigEntry<bool> DebugEvents;

    public override void Load()
    {
        Instance = this;
        Enabled = Config.Bind("General", "Enabled", true, "Enable gamepad vibration.");
        Strength = Config.Bind("General", "Strength", 0.7f, "Overall strength from 0 to 1.");
        VillageNavigation = Config.Bind("Events", "VillageNavigation", true, "Short ticks when changing or confirming a village selection.");
        DungeonBumps = Config.Bind("Events", "DungeonBumps", true, "Strong impacts for wall and reward-object bumps.");
        CardEffects = Config.Bind("Events", "CardEffects", true, "Continuous hard/soft weapon effects during their visual lifetime, with impact accents.");
        Rewards = Config.Bind("Events", "Rewards", true, "Coin and experience crystal scatter patterns during visual pickup streams.");
        EnemyAttacks = Config.Bind("Events", "EnemyAttacks", true, "Sustained rumble for recognized enemy spell animations and VFX.");
        PlayerHits = Config.Bind("Events", "PlayerHits", true, "Short impact at each enemy lunge apex or attack VFX spawn.");
        TestOnStart = Config.Bind("Diagnostics", "TestOnStart", false, "Send a single test pulse after a gamepad is detected.");
        DebugEvents = Config.Bind("Diagnostics", "DebugEvents", false, "Log haptic source events for troubleshooting.");

        ClassInjector.RegisterTypeInIl2Cpp<RumbleRunner>();
        var gameObject = new GameObject("VampireCrawlersRumble");
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        Runner = gameObject.AddComponent<RumbleRunner>();
        new Harmony(Id).PatchAll(typeof(Plugin).Assembly);
        Log.LogInfo("Rumble 0.2.5 loaded; waiting for a Unity Input System gamepad.");
    }

    [HarmonyPatch(typeof(TownViewController), nameof(TownViewController.SetCurrentSelection))]
    private static class VillageSelectionPatch
    {
        private static void Postfix() => Runner?.VillageSelection();
    }

    [HarmonyPatch(typeof(TownViewController), nameof(TownViewController.SelectionButtonPressed))]
    private static class VillageConfirmPatch
    {
        private static void Prefix() => Runner?.VillageConfirm();
    }

    [HarmonyPatch(typeof(Dungeon3DMapView), nameof(Dungeon3DMapView.PlayBumpAnimation))]
    private static class WallBumpPatch
    {
        private static void Prefix() => Runner?.WallBump();
    }

    [HarmonyPatch(typeof(DungeonPlayer), nameof(DungeonPlayer.TryBreakDestructible))]
    private static class RewardBumpPatch
    {
        private static void Postfix(bool __result)
        {
            if (__result) Runner?.RewardBump();
        }
    }

    [HarmonyPatch(typeof(CoinView), nameof(CoinView.SpawnAtDungeonPosition))]
    private static class CoinSpawnPatch
    {
        private static void Postfix() => Runner?.CoinSpawned();
    }

    [HarmonyPatch(typeof(CoinView), nameof(CoinView.OnReachedTarget))]
    private static class CoinArrivedPatch
    {
        private static void Postfix() => Runner?.CoinArrived();
    }

    [HarmonyPatch(typeof(CrystalEmitterView), nameof(CrystalEmitterView.RequestCrystalSpawn))]
    private static class CrystalSpawnPatch
    {
        private static void Postfix() => Runner?.CrystalSpawned();
    }

    [HarmonyPatch(typeof(CrystalEmitterView), nameof(CrystalEmitterView.CrystalReachedBar))]
    private static class CrystalArrivedPatch
    {
        private static void Postfix() => Runner?.CrystalArrived();
    }

    [HarmonyPatch]
    private static class WeaponSpawnPatch
    {
        private static IEnumerable<MethodBase> TargetMethods() => typeof(Weapon)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == nameof(Weapon.OnSpawned));

        private static void Postfix(Weapon __instance) => Runner?.WeaponStarted(__instance);
    }

    [HarmonyPatch(typeof(Weapon), nameof(Weapon.OnDisable))]
    private static class WeaponDisabledPatch
    {
        private static void Postfix(Weapon __instance) => Runner?.WeaponStopped(__instance);
    }

    [HarmonyPatch(typeof(WeaponCollision), nameof(WeaponCollision.DoDamageToTarget))]
    private static class WeaponImpactPatch
    {
        private static void Prefix(WeaponCollision __instance) => Runner?.WeaponImpact(__instance._weapon);
    }

    [HarmonyPatch(typeof(EnemyActionDamage), nameof(EnemyActionDamage.PlayViewAnimation))]
    private static class EnemyAttackPatch
    {
        private static void Prefix(EnemyActionDamage __instance, EnemyModel enemy, float durationMultiplier) =>
            Runner?.EnemyAttackVisualStarted(__instance, enemy, durationMultiplier);
    }

    [HarmonyPatch(typeof(EnemyAction), nameof(EnemyAction.Execute))]
    private static class EnemyActionExecutePatch
    {
        private static void Prefix(EnemyAction __instance, EnemyModel enemy, float durationMultiplier) =>
            Runner?.EnemyActionExecuting(__instance, enemy, durationMultiplier);
    }

    [HarmonyPatch(typeof(EnemyModel), nameof(EnemyModel.OnEnemyPerformedAction))]
    private static class EnemyPerformedActionProbePatch
    {
        private static void Prefix(EnemyAction action) => Runner?.EnemyActionPerformedProbe(action);
    }

    [HarmonyPatch(typeof(EnemyEncounterModel), nameof(EnemyEncounterModel.TryExecuteEnemies))]
    private static class EnemyTurnProbePatch
    {
        private static void Prefix() => Runner?.EnemyTurnProbe();
    }

    [HarmonyPatch(typeof(PlayerModel), nameof(PlayerModel.ResolveDamage))]
    private static class DamageProbePatch
    {
        private static void Prefix(PlayerModel __instance, int amount)
        {
            if (__instance.IsInEncounter && amount > 0) Runner?.DamageProbe(amount);
        }
    }

    [HarmonyPatch]
    private static class EnemyAnimationProbePatch
    {
        private static IEnumerable<MethodBase> TargetMethods() => typeof(EnemyView)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == nameof(EnemyView.PlayCustomAnimation));

        private static void Prefix(EnemyView __instance, AnimationClip clip) =>
            Runner?.EnemyAnimationProbe(__instance, clip);
    }

    [HarmonyPatch(typeof(EnemyActionDamage), nameof(EnemyActionDamage.DoDamageDelayed))]
    private static class EnemyVisualHitCuePatch
    {
        private static void Prefix(EnemyModel enemyModel, float waitTime) =>
            Runner?.EnemyVisualHitCueScheduled(enemyModel, waitTime);
    }

    [HarmonyPatch(typeof(EnemyActionMultiDamage), nameof(EnemyActionMultiDamage.OnUpdateView))]
    private static class EnemyMultiAttackPatch
    {
        private static void Prefix(EnemyActionMultiDamage __instance, EnemyModel enemy, float durationMultiplier) =>
            Runner?.EnemyMultiAttackVisualStarted(__instance, enemy, durationMultiplier);
    }

    [HarmonyPatch(typeof(EnemyActionMultiDamage), nameof(EnemyActionMultiDamage.TrySpawnVFX))]
    private static class EnemyMultiAttackVfxPatch
    {
        private static void Postfix(EnemyActionMultiDamage __instance, EnemyView enemyView) =>
            Runner?.EnemyAttackVfxStarted(__instance, enemyView);
    }

    [HarmonyPatch(typeof(EnemyEncounterModel), nameof(EnemyEncounterModel.OnEncounterEnded))]
    private static class EncounterEndedPatch
    {
        private static void Postfix() => Runner?.EndEncounter();
    }
}
