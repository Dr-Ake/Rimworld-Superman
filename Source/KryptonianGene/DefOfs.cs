using RimWorld;
using Verse;

namespace KryptonianGene
{
    [DefOf]
    public static class KryptonianDefOf
    {
        public static HediffDef KryptonianGene;
        public static HediffDef SolarExhaustion;

        static KryptonianDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(KryptonianDefOf));
        }
    }
}

