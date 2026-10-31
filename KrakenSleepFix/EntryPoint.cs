using BepInEx;
using BepInEx.Unity.IL2CPP;
using Enemies;
using HarmonyLib;
using UnityEngine;

namespace KrakenSleepFix;

[BepInPlugin("Amor.KrakenSleepFix", "KrakenSleepFix", "1.0.0")]
[BepInDependency("dev.gtfomodding.gtfo-api", BepInDependency.DependencyFlags.HardDependency)]
internal class EntryPoint : BasePlugin
{
    public override void Load()
    {
        new Harmony("Amor.KrakenSleepFix").PatchAll();
        Log.LogMessage("KrakenSleepFix is done loading!");
    }
}

[HarmonyPatch(typeof(EnemyPrefabManager), nameof(EnemyPrefabManager.BuildEnemyPrefab))]
internal static class KrakenSleepFix
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.LowerThanNormal)]
    [HarmonyWrapSafe]
    private static void Post_BuildPrefab(GameObject __result)
    {
        var krakenBehaviour = __result.GetComponent<SquidBossBehaviour>();
        if (krakenBehaviour == null)
            return;

        krakenBehaviour.HibernatingWakeupDistanceThreshold = 1000f;
    }
}
