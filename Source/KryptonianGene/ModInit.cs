using HarmonyLib;
using Verse;

namespace KryptonianGene
{
    public class KryptonianMod : Mod
    {
        public const string HarmonyId = "supers.kryptonian";

        public KryptonianMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
            Log.Message("[Kryptonian Gene] Harmony patches applied.");
        }
    }
}

