using RimWorld;
using Verse;

namespace KryptonianGene
{
    public class CompUseEffect_KryptonianGene : CompUseEffect
    {
        public override void DoEffect(Pawn user)
        {
            base.DoEffect(user);

            if (user == null)
            {
                return;
            }

            if (KryptonianUtility.HasKryptonianGene(user))
            {
                Messages.Message("KryptonianAlreadyHasGene".Translate(user.Named("PAWN")), user, MessageTypeDefOf.RejectInput, true);
                return;
            }

            Hediff hediff = HediffMaker.MakeHediff(KryptonianDefOf.KryptonianGene, user);
            user.health.AddHediff(hediff);

            CompKryptonianSolar comp = KryptonianUtility.GetSolarComp(user);
            if (comp != null)
            {
                comp.GainSolar(100f);
            }

            Messages.Message("KryptonianGeneApplied".Translate(user.Named("PAWN")), user, MessageTypeDefOf.PositiveEvent, true);
        }
    }
}

