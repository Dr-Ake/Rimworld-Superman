using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace KryptonianGene.Abilities
{
	/// <summary>
	/// Experimental rewrite of Kryptonian heat vision that channels the Beam Graser weapon internally.
	/// </summary>
	public class HeatVisionAbility
	{
		private readonly CompKryptonianSolar comp;

		private static readonly ThingDef BeamWeaponDef = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_BeamGraser");

		private const float MinimumSolar = 5f;

		private ThingWithComps activeEquipment;
		private Job queuedJob;
		private Verb currentVerb;

		public HeatVisionAbility(CompKryptonianSolar comp)
		{
			this.comp = comp ?? throw new ArgumentNullException(nameof(comp));
		}

		public IEnumerable<Gizmo> GetGizmos()
		{
			Pawn pawn = comp.Pawn;
			if (pawn == null || pawn.Dead)
			{
				yield break;
			}

			yield return new Command_Target
			{
				defaultLabel = "KryptonianHeatVisionMenu".Translate(),
				defaultDesc = "KryptonianHeatVisionMenuDesc".Translate(),
				icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Kryptonian/HeatVision", true),
				targetingParams = new TargetingParameters
				{
					canTargetLocations = true,
					canTargetPawns = true,
					canTargetBuildings = true,
					validator = target => target.IsValid && GenGrid.InBounds(target.Cell, pawn.Map)
				},
				action = target => TryCast(target)
			};
		}

		public void Tick()
		{
			if (queuedJob == null)
			{
				return;
			}

			Pawn pawn = comp.Pawn;
			if (pawn?.jobs == null)
			{
				Cleanup();
				return;
			}

			if (pawn.CurJob != queuedJob && !pawn.jobs.jobQueue.Contains(queuedJob))
			{
				Cleanup();
			}
		}

		public void Cancel(bool silent = false)
		{
			if (queuedJob == null)
			{
				return;
			}

			Pawn pawn = comp.Pawn;
			pawn?.jobs?.EndCurrentOrQueuedJob(queuedJob, JobCondition.InterruptOptional);
			Cleanup();

			if (!silent && pawn != null && pawn.Spawned)
			{
				SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, pawn.Map);
				Messages.Message("KryptonianHeatVisionTemporarilyDisabled".Translate(), pawn, MessageTypeDefOf.NeutralEvent);
			}
		}

		private void TryCast(LocalTargetInfo target)
		{
			Pawn pawn = comp.Pawn;
			if (pawn == null || pawn.Dead || pawn.Map == null)
			{
				return;
			}

			if (!target.IsValid || !GenGrid.InBounds(target.Cell, pawn.Map))
			{
				Messages.Message("CannotHitTarget".Translate(), pawn, MessageTypeDefOf.RejectInput, true);
				return;
			}

			if (comp.SolarCharge < MinimumSolar)
			{
				Messages.Message("KryptonianNotEnoughSolar".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.RejectInput, true);
				return;
			}

			if (!TryCreateVerb(pawn, out Verb verb))
			{
				Messages.Message("KryptonianHeatVisionMissingVerb".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.RejectInput, false);
				return;
			}

			Job job = JobMaker.MakeJob(JobDefOf.UseVerbOnThing, target);
			job.verbToUse = verb;
			job.playerForced = true;
			job.endIfCantShootTargetFromCurPos = true;
			job.count = 1;

			if (!pawn.jobs.TryTakeOrderedJob(job, JobTag.MiscWork))
			{
				Cleanup();
				return;
			}

			queuedJob = job;
			currentVerb = verb;
			comp.DrainSolar(MinimumSolar);
			SoundStarter.PlayOneShotOnCamera(SoundDefOf.EnergyShield_Reset, pawn.Map);
		}

		private bool TryCreateVerb(Pawn pawn, out Verb verb)
		{
			verb = null;
			if (BeamWeaponDef == null)
			{
				return false;
			}

			activeEquipment = ThingMaker.MakeThing(BeamWeaponDef) as ThingWithComps;
			if (activeEquipment == null)
			{
				return false;
			}
			activeEquipment.InitializeComps();

			CompEquippable equippable = activeEquipment.GetComp<CompEquippable>();
			if (equippable == null)
			{
				return false;
			}

			equippable.VerbTracker.InitVerbsFromZero();
			verb = equippable.VerbTracker.PrimaryVerb;
			if (verb == null)
			{
				return false;
			}

			verb.caster = pawn;
			verb.verbTracker = equippable.VerbTracker;
			return true;
		}

		private void Cleanup()
		{
			currentVerb = null;
			queuedJob = null;
			activeEquipment = null;
		}
	}
}
