using RimWorld;
using Verse;

namespace KryptonianGene
{
    public static class KryptonianUtility
    {
        public static CompKryptonianSolar GetSolarComp(Pawn pawn)
        {
            if (pawn?.health == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet?.GetFirstHediffOfDef(KryptonianDefOf.KryptonianGene);
            return hediff?.TryGetComp<CompKryptonianSolar>();
        }

        public static bool HasKryptonianGene(Pawn pawn)
        {
            return GetSolarComp(pawn) != null;
        }
    }
}

