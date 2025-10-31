using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace KryptonianGene
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos
    {
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            CompKryptonianSolar comp = KryptonianUtility.GetSolarComp(__instance);
            if (comp == null)
            {
                return;
            }

            List<Gizmo> list = __result.ToList();
            list.AddRange(comp.GetGizmos());
            __result = list;
        }
    }

    internal static class PawnPathFollowerFlightUtility
    {
        private static readonly System.Reflection.FieldInfo PawnField = AccessTools.Field(typeof(Pawn_PathFollower), "pawn");

        public static Pawn GetPawn(Pawn_PathFollower follower)
        {
            return follower == null ? null : (Pawn)PawnField.GetValue(follower);
        }

        public static bool IsFlying(Pawn_PathFollower follower, out Pawn pawn, out CompKryptonianSolar comp)
        {
            pawn = GetPawn(follower);
            comp = KryptonianUtility.GetSolarComp(pawn);
            return comp?.IsFlying == true;
        }
    }

    [HarmonyPatch(typeof(DamageWorker_AddInjury), "ApplyToPawn")]
    public static class Patch_DamageWorker_AddInjury_ApplyToPawn
    {
        public static void Prefix(ref DamageInfo dinfo, Pawn pawn)
        {
            CompKryptonianSolar comp = KryptonianUtility.GetSolarComp(pawn);
            if (comp == null)
            {
                return;
            }

            comp.TryPreDamage(ref dinfo);
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "IsNextCellWalkable")]
    public static class Patch_Pawn_PathFollower_IsNextCellWalkable
    {
        public static bool Prefix(Pawn_PathFollower __instance, ref bool __result)
        {
            if (!PawnPathFollowerFlightUtility.IsFlying(__instance, out Pawn pawn, out _))
            {
                return true;
            }

            if (pawn?.MapHeld == null)
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "PawnCanOccupy")]
    public static class Patch_Pawn_PathFollower_PawnCanOccupy
    {
        public static bool Prefix(Pawn_PathFollower __instance, IntVec3 c, ref bool __result)
        {
            if (!PawnPathFollowerFlightUtility.IsFlying(__instance, out Pawn pawn, out _))
            {
                return true;
            }

            Map map = pawn?.MapHeld;
            if (map == null || !c.InBounds(map))
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "CostToMoveIntoCell", new Type[] { typeof(IntVec3) })]
    public static class Patch_Pawn_PathFollower_CostToMoveIntoCell
    {
        public static void Postfix(Pawn_PathFollower __instance, IntVec3 c, ref float __result)
        {
            if (!PawnPathFollowerFlightUtility.IsFlying(__instance, out Pawn pawn, out _))
            {
                return;
            }

            if (pawn == null)
            {
                return;
            }

            float baseCost = pawn.TicksPerMoveCardinal;
            if (baseCost <= 0f)
            {
                baseCost = 1f;
            }

            __result = baseCost * 0.75f;
            if (__result < 1f)
            {
                __result = 1f;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "WillCollideWithPawnAt")]
    public static class Patch_Pawn_PathFollower_WillCollideWithPawnAt
    {
        public static bool Prefix(Pawn_PathFollower __instance, IntVec3 c, bool forceOnlyStanding, bool useId, ref bool __result)
        {
            if (!PawnPathFollowerFlightUtility.IsFlying(__instance, out _, out _))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.PostApplyDamage))]
    public static class Patch_PostApplyDamage
    {
        private static readonly System.Reflection.FieldInfo PawnField = AccessTools.Field(typeof(Pawn_HealthTracker), "pawn");

        public static void Postfix(Pawn_HealthTracker __instance, DamageInfo? dinfo, float totalDamageDealt)
        {
            Pawn pawn = (Pawn)PawnField.GetValue(__instance);
            CompKryptonianSolar comp = KryptonianUtility.GetSolarComp(pawn);
            if (comp == null)
            {
                return;
            }

            if (pawn.Dead && comp.SolarCharge > 0f)
            {
                comp.NotifyLethalDamagePrevented(dinfo ?? new DamageInfo());
                ResurrectionUtility.TryResurrect(pawn);
                pawn.health.Reset();
            }
        }
    }

    [HarmonyPatch(typeof(Corpse), nameof(Corpse.TickRare))]
    public static class Patch_Corpse_TickRare
    {
        public static void Postfix(Corpse __instance)
        {
            Pawn innerPawn = __instance.InnerPawn;
            CompKryptonianSolar comp = KryptonianUtility.GetSolarComp(innerPawn);
            comp?.HandleCorpseTick(__instance);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.LabelCap), MethodType.Getter)]
    public static class Patch_Corpse_LabelCap
    {
        public static void Postfix(Thing __instance, ref string __result)
        {
            if (__instance is not Corpse corpse)
            {
                return;
            }

            Pawn pawn = corpse.InnerPawn;
            if (pawn == null)
            {
                return;
            }

            if (!KryptonianUtility.HasKryptonianGene(pawn))
            {
                return;
            }

            string label = pawn.LabelShortCap;
            __result = string.Format("{0} ({1})", label, "KryptonianDormantLabel".Translate());
        }
    }

    [HarmonyPatch(typeof(Corpse), nameof(Corpse.GetInspectString))]
    public static class Patch_Corpse_GetInspectString
    {
        public static void Postfix(Corpse __instance, ref string __result)
        {
            Pawn pawn = __instance.InnerPawn;
            if (pawn == null || !KryptonianUtility.HasKryptonianGene(pawn))
            {
                return;
            }

            __result = "KryptonianDormantInspect".Translate();
        }
    }

    [HarmonyPatch(typeof(StatWorker), nameof(StatWorker.GetValueUnfinalized))]
    public static class Patch_StatWorker_GetValueUnfinalized
    {
        private static readonly AccessTools.FieldRef<StatWorker, StatDef> StatFieldRef = AccessTools.FieldRefAccess<StatWorker, StatDef>("stat");

        public static void Postfix(StatWorker __instance, StatRequest req, bool applyPostProcess, ref float __result)
        {
            if (!req.HasThing || req.Thing is not Pawn pawn)
            {
                return;
            }

            CompKryptonianSolar comp = KryptonianUtility.GetSolarComp(pawn);
            if (comp == null)
            {
                return;
            }

            StatDef stat = StatFieldRef(__instance);
            if (stat == null)
            {
                return;
            }

            float multiplier = comp.GetStatMultiplier(stat);
            __result *= multiplier;
        }
    }

    [HarmonyPatch(typeof(FireUtility), nameof(FireUtility.TryStartFireIn))]
    public static class Patch_FireUtility_TryStartFireIn
    {
        public static bool Prefix(IntVec3 c, Map map, float fireSize, ref bool __result)
        {
            if (!c.InBounds(map))
            {
                return true;
            }

            foreach (Thing thing in c.GetThingList(map))
            {
                if (thing is Pawn pawn && KryptonianUtility.HasKryptonianGene(pawn))
                {
                    __result = false;
                    return false;
                }

                if (thing is Corpse corpse && KryptonianUtility.HasKryptonianGene(corpse.InnerPawn))
                {
                    __result = false;
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(FireUtility), nameof(FireUtility.TryAttachFire))]
    public static class Patch_FireUtility_TryAttachFire
    {
        public static bool Prefix(Thing t, float fireSize, Thing instigator)
        {
            if (t is Pawn pawn && KryptonianUtility.HasKryptonianGene(pawn))
            {
                return false;
            }

            if (t is Corpse corpse && KryptonianUtility.HasKryptonianGene(corpse.InnerPawn))
            {
                return false;
            }

            return true;
        }
    }
}

