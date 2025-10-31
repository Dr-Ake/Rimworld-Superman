using RimWorld;
using UnityEngine;
using Verse;

namespace KryptonianGene
{
    public class Dialog_SupernovaRadius : Window
    {
        private readonly CompKryptonianSolar comp;
        private float radius;

        public override Vector2 InitialSize => new Vector2(420f, 200f);

        public Dialog_SupernovaRadius(CompKryptonianSolar comp)
        {
            this.comp = comp;
            radius = comp.SupernovaMinRadius;
            forcePause = true;
            doCloseButton = false;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "KryptonianSupernovaTitle".Translate());
            Text.Font = GameFont.Small;

            Rect sliderRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, 24f);
            radius = Widgets.HorizontalSlider(sliderRect, radius, comp.SupernovaMinRadius, comp.SupernovaMaxRadius, middleAlignment: true, label: "KryptonianSupernovaRadius".Translate(Mathf.RoundToInt(radius).ToString()));
            radius = Mathf.Clamp(radius, comp.SupernovaMinRadius, comp.SupernovaMaxRadius);

            Rect confirmRect = new Rect(inRect.x, inRect.yMax - 35f, 120f, 32f);
            if (Widgets.ButtonText(confirmRect, "Confirm".Translate()))
            {
                Close(doCloseSound: true);
                Find.WindowStack.Add(new Dialog_MessageBox("KryptonianSupernovaConfirm".Translate(radius.ToString("F0")), "Confirm".Translate(), () => comp.TriggerSupernova(radius), "Cancel".Translate()));
            }

            Rect cancelRect = new Rect(inRect.xMax - 120f, inRect.yMax - 35f, 120f, 32f);
            if (Widgets.ButtonText(cancelRect, "Cancel".Translate()))
            {
                Close();
            }
        }
    }
}

