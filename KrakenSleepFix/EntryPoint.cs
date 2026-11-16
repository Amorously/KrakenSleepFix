using BepInEx;
using BepInEx.Unity.IL2CPP;
using Enemies;
using GTFO.API;
using HarmonyLib;
using LevelGeneration;
using SNetwork;
using UnityEngine;

namespace KrakenSleepFix;

[BepInPlugin("Amor.KrakenSleepFix", "KrakenSleepFix", "1.1.0")]
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

[HarmonyPatch]
internal static class InteractTriggerFix // myCoolPlugin3
{
    private static bool s_Initialized = false;
    private static bool s_Modified = false;
    private static Transform? s_CarryAlignTransform;
    private static readonly Dictionary<ushort, ushort> s_ReplicatorLookups = new();
    private static readonly Dictionary<ushort, ushort> s_CheckpointLookups = new();    

    [HarmonyPrepare]
    private static void Prepare()
    {
        if (s_Initialized) return;
        s_Initialized = true;
        EventAPI.OnCheckpointReached += OnCheckpointReached;
        LevelAPI.OnLevelCleanup += OnLevelCleanup;        
    }

    private static void OnCheckpointReached()
    {
        s_Modified = false;
        s_CheckpointLookups.Clear();
        foreach ((ushort k, ushort v) in s_ReplicatorLookups)
            s_CheckpointLookups.Add(k, v);
    }

    private static void OnLevelCleanup()
    {
        s_CarryAlignTransform = null;
        s_ReplicatorLookups.Clear();
        s_CheckpointLookups.Clear();
        s_Modified = false;
    }

    [HarmonyPatch(typeof(LG_InteractWorldEventTrigger), nameof(LG_InteractWorldEventTrigger.OnStateChange))]
    [HarmonyPrefix]
    [HarmonyWrapSafe]
    private static void Pre_OnStateChange(LG_InteractWorldEventTrigger __instance, pSyncTriggerInteraction newState, bool isRecall)
    {        
        if (__instance.m_insertType == eCarryItemInsertTargetType.None || !__instance.m_removeItemOnInsert || isRecall)
            return;
        if (!CarryItemInteractionUtils.ValidateCarryItemInsertion(newState.TriggerSource, newState.TriggerItem, out _, out Item item))
            return;

        var itemInLevel = item.Cast<ItemInLevel>();
        var itemRep = itemInLevel.GetSyncComponent().GetReplicator();
        s_ReplicatorLookups.TryAdd(itemRep.Key, __instance.m_worldEventTriggerSync.m_stateReplicator.Replicator.Key);
        s_CarryAlignTransform = __instance.CarryItemAlign;
        s_Modified = true;

        if (SNet.IsMaster)
        {
            var data = item.GetCustomData();
            __instance.OnItemInserted(ref data);
            item.SetCustomData(data, false);
        }
    }

    [HarmonyPatch(typeof(CarryItemPickup_Core), nameof(CarryItemPickup_Core.OnSyncStateChange))]
    [HarmonyPostfix]
    [HarmonyWrapSafe]
    private static void Post_OnSyncStateChange(CarryItemPickup_Core __instance, ePickupItemStatus status, bool isRecall)
    {        
        if (isRecall)
        {
            if (s_Modified)
            {
                s_Modified = false;
                s_ReplicatorLookups.Clear();
                foreach ((ushort k, ushort v) in s_CheckpointLookups)
                    s_ReplicatorLookups.Add(k, v);
            }

            if (s_ReplicatorLookups.TryGetValue(__instance.m_sync.GetReplicator().Key, out var triggerRepKey))
            {
                if (SNet_Replication.TryGetReplicator(triggerRepKey, out var replicator))
                {
                    var interactTrigger = replicator.ReplicatorSupplier.gameObject.GetComponent<LG_InteractWorldEventTrigger>();
                    __instance.transform.SetParent(interactTrigger.CarryItemAlign, true);
                }
            }
            return;
        }

        if (status == ePickupItemStatus.PickedUp && s_ReplicatorLookups.Remove(__instance.m_sync.GetReplicator().Key))
            s_Modified = true;

        if (s_CarryAlignTransform != null)
        {
            __instance.transform.SetParent(s_CarryAlignTransform, true);
            s_CarryAlignTransform = null;
        }
    }
}
