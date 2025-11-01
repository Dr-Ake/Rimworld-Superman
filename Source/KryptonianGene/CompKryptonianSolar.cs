using System;
using System.Collections.Generic;
using KryptonianGene.Abilities;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace KryptonianGene;

	public class CompKryptonianSolar : HediffComp
	{
		private readonly struct HeatVisionProperties
		{
			public readonly float DrainPerSecond;

		public readonly float DamagePerSecond;

		public readonly float FireChancePerSecond;

		public readonly float ArmorPenetration;

		public readonly float BeamScale;

		public readonly float FireSize;

		public HeatVisionProperties(float drainPerSecond, float damagePerSecond, float fireChancePerSecond, float armorPenetration, float beamScale, float fireSize)
		{
			DrainPerSecond = drainPerSecond;
			DamagePerSecond = damagePerSecond;
			FireChancePerSecond = fireChancePerSecond;
			ArmorPenetration = armorPenetration;
			BeamScale = beamScale;
			FireSize = fireSize;
		}
	}

	private enum HeatVisionMode
	{
		Low,
		Medium,
		High,
		Lethal
	}

	private const float MaxSolarCharge = 100f;

	private const int ExhaustionCooldownTicks = 30000;

	private const float FlightDrainPerTick = 0.05f;

	private const float MinSupernovaRadius = 10f;

	private const float MaxSupernovaRadius = 79f;

	private static readonly ThingDef HeatVisionBeamThingDef = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_KryptonianHeatBeam");

	private static readonly VerbProperties BaseHeatVisionVerbProps = HeatVisionBeamThingDef?.verbs?.Count > 0 ? HeatVisionBeamThingDef.verbs[0] : null;

	private static readonly SoundDef BeamLoopSound = DefDatabase<SoundDef>.GetNamedSilentFail("BeamGraser_Shooting");

	private static readonly SoundDef BeamStopSound = DefDatabase<SoundDef>.GetNamedSilentFail("BeamGraser_Shooting_Resolve");

	private float solarCharge = 100f;

	private int solarCooldownTicks;

	private bool isFlying;

	private bool showingPowersMenu;

	private bool showingMiscMenu;

	private bool isExhausted;

	private ThingWithComps cachedHeatVisionBeam;

	private HeatVisionAbility heatVisionAbility;

	private HeatVisionAbility HeatVision => heatVisionAbility ??= new HeatVisionAbility(this);

	public Pawn Pawn => ((Hediff)base.parent).pawn;

	public float SolarCharge => solarCharge;

	public bool IsFlying => isFlying;

	public bool ShowingPowersMenu => showingPowersMenu;

	public bool ShowingMiscMenu => showingMiscMenu;

	public bool IsExhausted => isExhausted;

	public float SupernovaMinRadius => 10f;

	public float SupernovaMaxRadius => 79f;

	public override void CompExposeData()
	{
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Invalid comparison between Unknown and I4
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		((HediffComp)this).CompExposeData();
		Scribe_Values.Look<float>(ref solarCharge, "solarCharge", 100f, false);
		Scribe_Values.Look<int>(ref solarCooldownTicks, "solarCooldownTicks", 0, false);
		Scribe_Values.Look<bool>(ref isFlying, "isFlying", false, false);
		Scribe_Values.Look<bool>(ref showingPowersMenu, "showingPowersMenu", false, false);
		Scribe_Values.Look<bool>(ref showingMiscMenu, "showingMiscMenu", false, false);
		Scribe_Values.Look<bool>(ref isExhausted, "isExhausted", false, false);
		if (Scribe.mode == LoadSaveMode.PostLoadInit)
		{
			heatVisionAbility?.Cancel(silent: true);
		}
	}

	public override void CompPostTick(ref float severityAdjustment)
	{
		((HediffComp)this).CompPostTick(ref severityAdjustment);
		if (Pawn == null || Pawn.Dead)
		{
			heatVisionAbility?.Cancel(silent: true);
			return;
		}
		if (solarCooldownTicks > 0)
		{
			solarCooldownTicks--;
		}
		if (isFlying)
		{
			DrainSolar(0.05f);
			if (solarCharge <= 0f)
			{
				DisableFlight();
			}
		}
		if (!isExhausted && solarCharge <= 0f)
		{
			EnterExhaustion();
		}
		heatVisionAbility?.Tick();
		TryRechargeFromSunlight();
		ExitExhaustionIfReady();
	}

	private void TryRechargeFromSunlight()
	{
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		if (solarCharge >= 100f)
		{
			solarCharge = 100f;
			ExitExhaustionIfReady();
		}
		else if (solarCooldownTicks <= 0 && ((Thing)Pawn).Spawned && !GridsUtility.Roofed(((Thing)Pawn).Position, ((Thing)Pawn).Map))
		{
			float num = GenCelestial.CurCelestialSunGlow(((Thing)Pawn).Map);
			if (!(num <= 0f))
			{
				float num2 = num * 0.05f;
				solarCharge = Mathf.Min(100f, solarCharge + num2);
				ExitExhaustionIfReady();
			}
		}
	}

	private void EnterExhaustion()
	{
		if (!isExhausted)
		{
			isExhausted = true;
			solarCooldownTicks = 30000;
			AddExhaustionHediff();
			StopHeatVision(silent: true);
		}
	}

	private void AddExhaustionHediff()
	{
		if (Pawn != null && !Pawn.health.hediffSet.HasHediff(KryptonianDefOf.SolarExhaustion, false))
		{
			Pawn.health.AddHediff(KryptonianDefOf.SolarExhaustion, (BodyPartRecord)null, (DamageInfo?)null, (DamageResult)null);
		}
	}

	private void RemoveExhaustionHediff()
	{
		if (Pawn != null)
		{
			Hediff firstHediffOfDef = Pawn.health.hediffSet.GetFirstHediffOfDef(KryptonianDefOf.SolarExhaustion, false);
			if (firstHediffOfDef != null)
			{
				Pawn.health.RemoveHediff(firstHediffOfDef);
			}
		}
	}

	private void ExitExhaustionIfReady()
	{
		if (isExhausted && solarCooldownTicks <= 0 && !(solarCharge <= 0f))
		{
			isExhausted = false;
			RemoveExhaustionHediff();
		}
	}

	public void DrainSolar(float amount)
	{
		if (!(amount <= 0f))
		{
			solarCharge = Mathf.Max(0f, solarCharge - amount);
			if (solarCharge <= 0f)
			{
				EnterExhaustion();
				heatVisionAbility?.Cancel(silent: true);
			}
		}
	}

	public void GainSolar(float amount)
	{
		if (!(amount <= 0f) && solarCooldownTicks <= 0)
		{
			float num = solarCharge;
			solarCharge = Mathf.Clamp(solarCharge + amount, 0f, 100f);
			if (solarCharge > num)
			{
				ExitExhaustionIfReady();
			}
		}
	}

	public void EnableFlight()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (solarCharge <= 0f)
		{
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("KryptonianFlightNoEnergy", NamedArgument.op_Implicit(((Entity)Pawn).LabelShort))), LookTargets.op_Implicit((Thing)(object)Pawn), MessageTypeDefOf.RejectInput, true);
		}
		else
		{
			isFlying = true;
		}
	}

	public void DisableFlight()
	{
		if (isFlying)
		{
			isFlying = false;
		}
	}

	public void ToggleFlight()
	{
		if (isFlying)
		{
			DisableFlight();
		}
		else
		{
			EnableFlight();
		}
	}

	public IEnumerable<Gizmo> GetGizmos()
	{
		if (Pawn == null || Pawn.Dead)
		{
			yield break;
		}
		string chargeLabel = $"{Mathf.RoundToInt(solarCharge)}%%";
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("KryptonianPowers", NamedArgument.op_Implicit(chargeLabel))),
			defaultDesc = TaggedString.op_Implicit(Translator.Translate("KryptonianPowersDesc")),
			icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Kryptonian/Powers", false),
			action = delegate
			{
				showingPowersMenu = !showingPowersMenu;
				if (!showingPowersMenu)
				{
					showingMiscMenu = false;
					heatVisionAbility?.Cancel(silent: true);
				}
			}
		};
		if (!showingPowersMenu)
		{
			yield break;
		}
		foreach (Gizmo powerSubmenuGizmo in GetPowerSubmenuGizmos())
		{
			yield return powerSubmenuGizmo;
		}
	}

	private IEnumerable<Gizmo> GetPowerSubmenuGizmos()
	{
		foreach (Gizmo heatVisionGizmo in HeatVision.GetGizmos())
		{
			yield return heatVisionGizmo;
		}
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(isFlying ? Translator.Translate("KryptonianFlightDisable") : Translator.Translate("KryptonianFlightEnable")),
			defaultDesc = TaggedString.op_Implicit(Translator.Translate("KryptonianFlightDesc")),
			icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Kryptonian/Flight", false),
			action = ToggleFlight
		};
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(Translator.Translate("KryptonianIceBreath")),
			defaultDesc = TaggedString.op_Implicit(Translator.Translate("KryptonianIceBreathDesc")),
			icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Kryptonian/IceBreath", false),
			action = BeginIceBreathTargeting
		};
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(Translator.Translate("KryptonianMiscMenu")),
			defaultDesc = TaggedString.op_Implicit(Translator.Translate("KryptonianMiscMenuDesc")),
			icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Kryptonian/Misc", false),
			action = delegate
			{
				showingMiscMenu = !showingMiscMenu;
			}
		};
		if (!showingMiscMenu)
		{
			yield break;
		}
		foreach (Gizmo miscGizmo in GetMiscGizmos())
		{
			yield return miscGizmo;
		}
	}

	private IEnumerable<Gizmo> GetHeatVisionGizmos()
	{
		yield return (Gizmo)(object)MakeHeatVisionCommand("KryptonianHeatVisionLow", HeatVisionMode.Low);
		yield return (Gizmo)(object)MakeHeatVisionCommand("KryptonianHeatVisionMedium", HeatVisionMode.Medium);
		yield return (Gizmo)(object)MakeHeatVisionCommand("KryptonianHeatVisionHigh", HeatVisionMode.High);
		yield return (Gizmo)(object)MakeHeatVisionCommand("KryptonianHeatVisionLethal", HeatVisionMode.Lethal);
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(Translator.Translate("KryptonianBack")),
			defaultDesc = TaggedString.op_Implicit(Translator.Translate("KryptonianBackDesc")),
			action = delegate
			{
				showingHeatVisionMenu = false;
				StopHeatVision(silent: true);
			}
		};
	}

	private IEnumerable<Gizmo> GetMiscGizmos()
	{
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(Translator.Translate("KryptonianCellophaneS")),
			defaultDesc = TaggedString.op_Implicit(Translator.Translate("KryptonianCellophaneSDesc")),
			icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Kryptonian/CellophaneS", false),
			action = BeginCellophaneTargeting
		};
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(Translator.Translate("KryptonianXRayVision")),
			defaultDesc = TaggedString.op_Implicit(Translator.Translate("KryptonianXRayVisionDesc")),
			icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Kryptonian/XRay", false),
			action = ActivateXRayVision
		};
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(Translator.Translate("KryptonianSupernova")),
			defaultDesc = TaggedString.op_Implicit(Translator.Translate("KryptonianSupernovaDesc")),
			icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Kryptonian/Supernova", false),
			action = OpenSupernovaDialog
		};
		yield return (Gizmo)new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(Translator.Translate("KryptonianBack")),
			defaultDesc = TaggedString.op_Implicit(Translator.Translate("KryptonianBackDesc")),
			action = delegate
			{
				showingMiscMenu = false;
			}
		};
	}

	private Command_Action MakeHeatVisionCommand(string labelKey, HeatVisionMode mode)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Expected O, but got Unknown
		return new Command_Action
		{
			defaultLabel = TaggedString.op_Implicit(Translator.Translate(labelKey)),
			defaultDesc = TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("KryptonianHeatVisionDesc", NamedArgument.op_Implicit(GetHeatVisionLabel(mode)))),
			icon = (Texture)(object)ContentFinder<Texture2D>.Get("UI/Kryptonian/HeatVision", false),
			action = delegate
			{
				if (heatVisionActive && activeHeatVisionMode == mode)
				{
					StopHeatVision();
				}
				else
				{
					BeginHeatVisionTargeting(mode);
				}
			}
		};
	}

	private static string GetHeatVisionLabel(HeatVisionMode mode)
	{
		return mode switch
		{
			HeatVisionMode.Low => "low", 
			HeatVisionMode.Medium => "medium", 
			HeatVisionMode.High => "high", 
			HeatVisionMode.Lethal => "lethal", 
			_ => "low", 
		};
	}

	private void BeginHeatVisionTargeting(HeatVisionMode mode)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Expected O, but got Unknown
		if (heatVisionActive)
		{
			StopHeatVision(silent: true);
		}
		if (!HasSolarForHeatVision(mode))
		{
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("KryptonianNotEnoughSolar", NamedArgument.op_Implicit(((Entity)Pawn).LabelShort))), LookTargets.op_Implicit((Thing)(object)Pawn), MessageTypeDefOf.RejectInput, true);
		}
		else
		{
			if (!((Thing)Pawn).Spawned || ((Thing)Pawn).Map == null)
			{
				return;
			}
			TargetingParameters val = new TargetingParameters
			{
				canTargetLocations = true,
				canTargetPawns = true,
				canTargetBuildings = true,
				validator = (TargetInfo target) => GenGrid.InBounds(target.Cell, ((Thing)Pawn).Map)
			};
			Find.Targeter.BeginTargeting(val, (Action<LocalTargetInfo>)delegate(LocalTargetInfo target)
			{
				//IL_0020: Unknown result type (might be due to invalid IL or missing references)
				if (target.IsValid)
				{
					StartHeatVision(mode, target.Cell);
				}
			}, Pawn, (Action)null, (Texture2D)null, true);
		}
	}

	private bool HasSolarForHeatVision(HeatVisionMode mode)
	{
		HeatVisionProperties heatVisionProperties = GetHeatVisionProperties(mode);
		return solarCharge >= heatVisionProperties.DrainPerSecond;
	}

	private void StartHeatVision(HeatVisionMode mode, IntVec3 targetCell)
	{
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		if (!((Thing)Pawn).Spawned || ((Thing)Pawn).Map == null)
		{
			return;
		}
		HeatVisionProperties heatVisionProperties = GetHeatVisionProperties(mode);
		if (solarCharge < heatVisionProperties.DrainPerSecond)
		{
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("KryptonianNotEnoughSolar", NamedArgument.op_Implicit(((Entity)Pawn).LabelShort))), LookTargets.op_Implicit((Thing)(object)Pawn), MessageTypeDefOf.RejectInput, true);
			return;
		}
		StopHeatVision(silent: true);
		activeHeatVisionMode = mode;
		heatVisionActive = true;
		heatVisionTargetCell = (GenGrid.InBounds(targetCell, ((Thing)Pawn).Map) ? targetCell : ((Thing)Pawn).Position);
		heatVisionInputCaptured = false;
		heatVisionWindow = (Window)(object)new HeatVisionControllerWindow(this);
		Find.WindowStack.Add(heatVisionWindow);
		Pawn.stances.CancelBusyStanceSoft();
		Pawn_PathFollower pather = Pawn.pather;
		if (pather != null)
		{
			pather.StopDead();
		}
		Pawn_PathFollower pather2 = Pawn.pather;
		if (pather2 != null)
		{
			pather2.ResetToCurrentPosition();
		}
		if (BeamLoopSound != null)
		{
			heatVisionSustainer = SoundStarter.TrySpawnSustainer(BeamLoopSound, SoundInfo.InMap(new TargetInfo(((Thing)Pawn).Position, ((Thing)Pawn).Map, false), (MaintenanceType)1));
		}
		Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("KryptonianHeatVisionActive", NamedArgument.op_Implicit(((Entity)Pawn).LabelShort))), LookTargets.op_Implicit((Thing)(object)Pawn), MessageTypeDefOf.PositiveEvent, false);
	}

	private void SetHeatVisionTarget(IntVec3 cell)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		if (heatVisionActive)
		{
			Pawn pawn = Pawn;
			if (((pawn != null) ? ((Thing)pawn).Map : null) != null && GenGrid.InBounds(cell, ((Thing)Pawn).Map))
			{
				heatVisionTargetCell = cell;
			}
		}
	}

	private void StopHeatVision(bool silent = false)
	{
		if (heatVisionAbility != null)
		{
			heatVisionAbility.Cancel(silent);
		}
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		if (heatVisionActive)
		{
			heatVisionActive = false;
			heatVisionInputCaptured = false;
			activeHeatVisionMode = HeatVisionMode.Low;
			heatVisionTargetCell = IntVec3.Invalid;
			if (heatVisionSustainer != null)
			{
				heatVisionSustainer.End();
				heatVisionSustainer = null;
			}
			if (!silent && BeamStopSound != null && Pawn != null && ((Thing)Pawn).Spawned)
			{
				SoundStarter.PlayOneShot(BeamStopSound, SoundInfo.InMap(new TargetInfo(((Thing)Pawn).Position, ((Thing)Pawn).Map, false), (MaintenanceType)0));
			}
			if (heatVisionWindow != null)
			{
				Find.WindowStack.TryRemove(heatVisionWindow, false);
				heatVisionWindow = null;
			}
			if (!silent && Pawn != null && ((Thing)Pawn).Spawned)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("KryptonianHeatVisionEnded", NamedArgument.op_Implicit(((Entity)Pawn).LabelShort))), LookTargets.op_Implicit((Thing)(object)Pawn), MessageTypeDefOf.NeutralEvent, false);
			}
		}
	}

	private void UpdateHeatVisionBeam()
	{
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0310: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		if (!heatVisionActive)
		{
			return;
		}
		try
		{
			if (!((Thing)Pawn).Spawned || ((Thing)Pawn).Map == null || Pawn.Dead || Pawn.Downed)
			{
				StopHeatVision(silent: true);
				return;
			}
			HeatVisionProperties heatVisionProperties = GetHeatVisionProperties(activeHeatVisionMode);
			float num = heatVisionProperties.DrainPerSecond / 60f;
			if (solarCharge < num)
			{
				StopHeatVision();
				return;
			}
			DrainSolar(num);
			if (!heatVisionActive)
			{
				return;
			}
			Map map = ((Thing)Pawn).Map;
			if (map == null)
			{
				StopHeatVision(silent: true);
				return;
			}
			IntVec3 position = ((Thing)Pawn).Position;
			IntVec3 val = (GenGrid.InBounds(heatVisionTargetCell, map) ? heatVisionTargetCell : position);
			if (val == position)
			{
				return;
			}
			Pawn_RotationTracker rotationTracker = Pawn.rotationTracker;
			if (rotationTracker != null)
			{
				rotationTracker.FaceCell(val);
			}
			IEnumerable<IntVec3> enumerable = EnumerateBeamCells(position, val);
			float num2 = heatVisionProperties.DamagePerSecond / 60f;
			float num3 = heatVisionProperties.FireChancePerSecond / 60f;
			int ticksGame = Find.TickManager.TicksGame;
			Sustainer obj = heatVisionSustainer;
			if (obj != null)
			{
				obj.Maintain();
			}
			DamageInfo val4 = default(DamageInfo);
			foreach (IntVec3 item in enumerable)
			{
				IntVec3 current = item;
				if (!GenGrid.InBounds(current, map) || current == position)
				{
					continue;
				}
				if ((ticksGame + current.x + current.z) % 2 == 0)
				{
					Vector3 val2 = current.ToVector3Shifted();
					FleckMaker.Static(val2, map, BeamGlowFleck ?? FleckDefOf.FireGlow, heatVisionProperties.BeamScale);
				}
				List<Thing> thingList = GridsUtility.GetThingList(current, map);
				if (thingList != null)
				{
					for (int i = 0; i < thingList.Count; i++)
					{
						Thing val3 = thingList[i];
						if (val3 == null || val3.Destroyed || val3 == Pawn)
						{
							continue;
						}
						val4._002Ector(DamageDefOf.Flame, num2, heatVisionProperties.ArmorPenetration, -1f, (Thing)(object)Pawn, (BodyPartRecord)null, (ThingDef)null, (SourceCategory)0, (Thing)null, true, true, (QualityCategory)2, true, false);
						val3.TakeDamage(val4);
						Pawn val5 = (Pawn)(object)((val3 is Pawn) ? val3 : null);
						if (val5 == null)
						{
							continue;
						}
						Pawn_StanceTracker stances = val5.stances;
						if (stances != null)
						{
							StunHandler stunner = stances.stunner;
							if (stunner != null)
							{
								stunner.StunFor(15, (Thing)(object)Pawn, true, true, false);
							}
						}
					}
				}
				if (num3 > 0f && Rand.Chance(num3))
				{
					FireUtility.TryStartFireIn(current, map, heatVisionProperties.FireSize, (Thing)(object)Pawn, (SimpleCurve)null);
					if (BeamBurnFleck != null)
					{
						FleckMaker.Static(current.ToVector3Shifted(), map, BeamBurnFleck ?? FleckDefOf.Smoke, Mathf.Max(0.8f, heatVisionProperties.FireSize));
					}
				}
			}
		}
		catch (Exception arg2)
		{
			Pawn pawn = Pawn;
			string arg = ((pawn != null) ? ((Entity)pawn).LabelShort : null) ?? "null pawn";
			Log.ErrorOnce($"[Kryptonian HeatVision] Exception for {arg}: {arg2}", ((object)this).GetHashCode() ^ 0x3A5F17);
			StopHeatVision(silent: true);
		}
	}

	private IEnumerable<IntVec3> EnumerateBeamCells(IntVec3 start, IntVec3 end)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		IntVec3 current = start;
		int dx = Mathf.Abs(end.x - start.x);
		int dz = Mathf.Abs(end.z - start.z);
		int sx = ((start.x < end.x) ? 1 : (-1));
		int sz = ((start.z < end.z) ? 1 : (-1));
		int err = dx - dz;
		int safety = 0;
		while (true)
		{
			yield return current;
			if (current == end || safety++ > 256)
			{
				break;
			}
			int e2 = err * 2;
			if (e2 > -dz)
			{
				err -= dz;
				current.x += sx;
			}
			if (e2 < dx)
			{
				err += dx;
				current.z += sz;
			}
		}
	}

	private HeatVisionProperties GetHeatVisionProperties(HeatVisionMode mode)
	{
		return mode switch
		{
			HeatVisionMode.Low => new HeatVisionProperties(0.1f, 18f, 0.25f, 0.2f, 0.6f, 0.18f), 
			HeatVisionMode.Medium => new HeatVisionProperties(0.3f, 40f, 0.5f, 0.35f, 0.8f, 0.28f), 
			HeatVisionMode.High => new HeatVisionProperties(0.7f, 80f, 0.8f, 0.55f, 1.05f, 0.45f), 
			HeatVisionMode.Lethal => new HeatVisionProperties(1.5f, 150f, 1f, 0.9f, 1.25f, 0.65f), 
			_ => new HeatVisionProperties(0.1f, 18f, 0.25f, 0.2f, 0.6f, 0.18f), 
		};
	}

	private void BeginIceBreathTargeting()
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Expected O, but got Unknown
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		if (solarCharge < 0.5f)
		{
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("KryptonianNotEnoughSolar", NamedArgument.op_Implicit(((Entity)Pawn).LabelShort))), LookTargets.op_Implicit((Thing)(object)Pawn), MessageTypeDefOf.RejectInput, true);
			return;
		}
		TargetingParameters val = new TargetingParameters
		{
			canTargetLocations = true,
			canTargetPawns = true,
			canTargetBuildings = true,
			validator = (TargetInfo target) => GenGrid.InBounds(target.Cell, ((Thing)Pawn).Map)
		};
		Find.Targeter.BeginTargeting(val, (Action<LocalTargetInfo>)delegate(LocalTargetInfo target)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			CastIceBreath(target);
		}, Pawn, (Action)null, (Texture2D)null, true);
	}

	private void CastIceBreath(LocalTargetInfo target)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		if (!target.IsValid || Pawn == null || ((Thing)Pawn).Map == null)
		{
			return;
		}
		Vector3 val = target.CenterVector3 - GenThing.TrueCenter((Thing)(object)Pawn);
		Vector3 normalized = val.normalized;
		foreach (IntVec3 item in GenRadial.RadialCellsAround(((Thing)Pawn).Position, 7f, true))
		{
			IntVec3 current = item;
			if (!GenGrid.InBounds(current, ((Thing)Pawn).Map))
			{
				continue;
			}
			val = current.ToVector3Shifted() - GenThing.TrueCenter((Thing)(object)Pawn);
			Vector3 normalized2 = val.normalized;
			float num = Vector3.Angle(normalized, normalized2);
			if (num > 45f || !GenSight.LineOfSight(((Thing)Pawn).Position, current, ((Thing)Pawn).Map))
			{
				continue;
			}
			GenTemperature.PushHeat(current, ((Thing)Pawn).Map, -30f);
			Thing firstThing = GridsUtility.GetFirstThing(current, ((Thing)Pawn).Map, ThingDefOf.Fire);
			Fire val2 = (Fire)(object)((firstThing is Fire) ? firstThing : null);
			if (val2 != null)
			{
				((Thing)val2).Destroy((DestroyMode)0);
			}
			List<Thing> thingList = GridsUtility.GetThingList(current, ((Thing)Pawn).Map);
			for (int i = 0; i < thingList.Count; i++)
			{
				Thing obj = thingList[i];
				Pawn val3 = (Pawn)(object)((obj is Pawn) ? obj : null);
				if (val3 != null && val3 != Pawn)
				{
					val3.stances.stunner.StunFor(180, (Thing)(object)Pawn, true, true, false);
					Hediff firstHediffOfDef = val3.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia, false);
					if (firstHediffOfDef != null)
					{
						firstHediffOfDef.Severity = Mathf.Clamp(firstHediffOfDef.Severity + 0.1f, 0f, 1f);
						continue;
					}
					Hediff val4 = HediffMaker.MakeHediff(HediffDefOf.Hypothermia, val3, (BodyPartRecord)null);
					val4.Severity = 0.1f;
					val3.health.AddHediff(val4, (BodyPartRecord)null, (DamageInfo?)null, (DamageResult)null);
				}
			}
		}
		DrainSolar(0.5f);
	}

	private void BeginCellophaneTargeting()
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Expected O, but got Unknown
		if (solarCharge < 2f)
		{
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("KryptonianNotEnoughSolar", NamedArgument.op_Implicit(((Entity)Pawn).LabelShort))), LookTargets.op_Implicit((Thing)(object)Pawn), MessageTypeDefOf.RejectInput, true);
			return;
		}
		TargetingParameters val = new TargetingParameters
		{
			canTargetPawns = true,
			canTargetLocations = false,
			validator = (TargetInfo target) => target.Thing is Pawn
		};
		Find.Targeter.BeginTargeting(val, (Action<LocalTargetInfo>)delegate(LocalTargetInfo target)
		{
			if (target.Pawn != null)
			{
				target.Pawn.stances.stunner.StunFor(300, (Thing)(object)Pawn, true, true, false);
				DrainSolar(2f);
			}
		}, Pawn, (Action)null, (Texture2D)null, true);
	}

	private void ActivateXRayVision()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		if (solarCharge < 1f)
		{
			Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("KryptonianNotEnoughSolar", NamedArgument.op_Implicit(((Entity)Pawn).LabelShort))), LookTargets.op_Implicit((Thing)(object)Pawn), MessageTypeDefOf.RejectInput, true);
			return;
		}
		Map map = ((Thing)Pawn).Map;
		if (map == null)
		{
			return;
		}
		foreach (IntVec3 item in GenRadial.RadialCellsAround(((Thing)Pawn).Position, 30f, true))
		{
			if (GenGrid.InBounds(item, map))
			{
				map.fogGrid.Unfog(item);
			}
		}
		DrainSolar(1f);
		Pawn_PathFollower pather = Pawn.pather;
		if (pather != null)
		{
			pather.StopDead();
		}
		Pawn_PathFollower pather2 = Pawn.pather;
		if (pather2 != null)
		{
			pather2.ResetToCurrentPosition();
		}
	}

	private void OpenSupernovaDialog()
	{
		Find.WindowStack.Add((Window)(object)new Dialog_SupernovaRadius(this));
	}

	public void TriggerSupernova(float radius)
	{
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		if (Pawn != null && ((Thing)Pawn).Map != null)
		{
			radius = Mathf.Clamp(radius, 10f, 79f);
			if (solarCharge <= 0f)
			{
				Messages.Message(TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("KryptonianNotEnoughSolar", NamedArgument.op_Implicit(((Entity)Pawn).LabelShort))), LookTargets.op_Implicit((Thing)(object)Pawn), MessageTypeDefOf.RejectInput, true);
			}
			else
			{
				ActionsDoSupernova(((Thing)Pawn).Position, radius);
			}
		}
	}

	private void ActionsDoSupernova(IntVec3 center, float radius)
	{
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0262: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_0302: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_032c: Unknown result type (might be due to invalid IL or missing references)
		radius = Mathf.Clamp(radius, 10f, 79f);
		Map map = ((Thing)Pawn).Map;
		if (((Thing)Pawn).Spawned)
		{
			Vector3 val = center.ToVector3Shifted();
			FleckMaker.Static(val, map, FleckDefOf.PsycastAreaEffect, radius);
			FleckMaker.Static(val, map, FleckDefOf.LightningGlow, Mathf.Max(6f, radius));
			SoundStarter.PlayOneShotOnCamera(SoundDefOf.PsychicPulseGlobal, map);
		}
		float num = radius * 0.6f;
		float num2 = Mathf.Min(radius * 1.2f, 79f);
		GenExplosion.DoExplosion(center, map, radius, DamageDefOf.Bomb, (Thing)(object)Pawn, 500, 1f, (SoundDef)null, (ThingDef)null, (ThingDef)null, (Thing)null, ThingDefOf.Filth_Ash, 0.6f, 1, (GasType?)(GasType)0, (float?)null, 1, true, (ThingDef)null, 0f, 1, 0f, false, (float?)null, (List<Thing>)null, (FloatRange?)null, true, 1f, 0f, true, (ThingDef)null, 1f, (SimpleCurve)null, (List<IntVec3>)null, (ThingDef)null, (ThingDef)null);
		TerrainDef val2 = DefDatabase<TerrainDef>.GetNamedSilentFail("BurnedGround") ?? DefDatabase<TerrainDef>.GetNamedSilentFail("BurntGround");
		DamageInfo val5 = default(DamageInfo);
		foreach (IntVec3 item in GenRadial.RadialCellsAround(center, num2, true))
		{
			IntVec3 current = item;
			if (!GenGrid.InBounds(current, map))
			{
				continue;
			}
			float num3 = IntVec3Utility.DistanceTo(center, current);
			if (num3 <= num)
			{
				List<Thing> thingList = GridsUtility.GetThingList(current, map);
				for (int num4 = thingList.Count - 1; num4 >= 0; num4--)
				{
					Thing val3 = thingList[num4];
					if (val3 != Pawn)
					{
						Pawn val4 = (Pawn)(object)((val3 is Pawn) ? val3 : null);
						if (val4 != null)
						{
							val5._002Ector(DamageDefOf.Flame, 9999f, 10f, -1f, (Thing)(object)Pawn, (BodyPartRecord)null, (ThingDef)null, (SourceCategory)0, (Thing)null, true, true, (QualityCategory)2, true, false);
							((Thing)val4).TakeDamage(val5);
						}
						else if (val3.def.destroyable)
						{
							val3.Destroy((DestroyMode)0);
						}
					}
				}
				if (val2 != null)
				{
					map.terrainGrid.SetTerrain(current, val2);
				}
				FilthMaker.TryMakeFilth(current, map, ThingDefOf.Filth_Ash, 3, (FilthSourceFlags)0, true);
			}
			else if (num3 <= radius)
			{
				FireUtility.TryStartFireIn(current, map, 1.2f, (Thing)(object)Pawn, (SimpleCurve)null);
				GenTemperature.PushHeat(current, map, 2000f);
				FleckMaker.Static(current.ToVector3Shifted(), map, FleckDefOf.Smoke, 2f);
			}
			else
			{
				GenTemperature.PushHeat(current, map, 500f);
			}
		}
		foreach (IntVec3 item2 in GenRadial.RadialCellsAround(center, radius, true))
		{
			if (GenGrid.InBounds(item2, map))
			{
				Thing firstThing = GridsUtility.GetFirstThing(item2, map, ThingDefOf.Uranium);
				if (firstThing != null && Rand.Chance(0.2f))
				{
					GenExplosion.DoExplosion(item2, map, 5f, DamageDefOf.Flame, (Thing)(object)Pawn, 300, -1f, (SoundDef)null, (ThingDef)null, (ThingDef)null, (Thing)null, (ThingDef)null, 0f, 1, (GasType?)null, (float?)null, 255, false, (ThingDef)null, 0f, 1, 0f, false, (float?)null, (List<Thing>)null, (FloatRange?)null, true, 1f, 0f, true, (ThingDef)null, 1f, (SimpleCurve)null, (List<IntVec3>)null, (ThingDef)null, (ThingDef)null);
				}
			}
		}
		DrainSolar(solarCharge);
		EnterExhaustion();
		Pawn.stances.stunner.StunFor(600, (Thing)(object)Pawn, true, true, false);
		if (!((Thing)Pawn).Spawned)
		{
			return;
		}
		CameraDriver cameraDriver = Find.CameraDriver;
		if (cameraDriver != null)
		{
			CameraShaker shaker = cameraDriver.shaker;
			if (shaker != null)
			{
				shaker.DoShake(Mathf.Clamp(radius / 20f, 1f, 5f));
			}
		}
	}

	public float GetStatMultiplier(StatDef stat)
	{
		if (Pawn == null)
		{
			return 1f;
		}
		if (solarCharge <= 0f || isExhausted)
		{
			return 0.5f;
		}
		if (stat == StatDefOf.MeleeDPS)
		{
			return 4f;
		}
		if (stat == StatDefOf.CarryingCapacity)
		{
			return 3f;
		}
		if (stat == StatDefOf.MiningSpeed)
		{
			return 3f;
		}
		if (stat == StatDefOf.MoveSpeed)
		{
			float num = 1.5f;
			if (isFlying)
			{
				num *= 1.5f;
			}
			return num;
		}
		return 1f;
	}

	public void TryPreDamage(ref DamageInfo dinfo)
	{
		if (Pawn == null || dinfo.Def == null || solarCharge <= 0f)
		{
			return;
		}
		DamageDef def = dinfo.Def;
		if (IsHeatOrFireDamage(def) || def.isExplosive)
		{
			dinfo.SetAmount(0f);
			return;
		}
		if (dinfo.Amount < 80f)
		{
			dinfo.SetAmount(0f);
			return;
		}
		float amount = Mathf.Min(dinfo.Amount * 0.05f, 10f);
		DrainSolar(amount);
		float num = dinfo.Amount - 80f;
		if (num <= 0f)
		{
			dinfo.SetAmount(0f);
		}
		else
		{
			dinfo.SetAmount(num);
		}
	}

	private static bool IsHeatOrFireDamage(DamageDef damageDef)
	{
		if (damageDef == null)
		{
			return false;
		}
		if (damageDef == DamageDefOf.Flame || damageDef == DamageDefOf.Burn || damageDef == DamageDefOf.AcidBurn || damageDef == DamageDefOf.Vaporize)
		{
			return true;
		}
		if (damageDef.explosionHeatEnergyPerCell > 0f)
		{
			return true;
		}
		return false;
	}

	public void NotifyLethalDamagePrevented(DamageInfo dinfo)
	{
		DrainSolar(solarCharge);
		EnterExhaustion();
	}

	public void HandleCorpseTick(Corpse corpse)
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		if (corpse == null || ((Thing)corpse).Destroyed || ((Thing)corpse).Map == null)
		{
			return;
		}
		CompRottable comp = ((ThingWithComps)corpse).GetComp<CompRottable>();
		if (comp != null)
		{
			comp.RotProgress = 0f;
		}
		corpse.Age = 0;
		if (GridsUtility.Roofed(((Thing)corpse).Position, ((Thing)corpse).Map))
		{
			return;
		}
		float num = GenCelestial.CurCelestialSunGlow(((Thing)corpse).Map);
		if (!(num <= 0f))
		{
			float num2 = num * 0.05f * 250f;
			solarCharge = Mathf.Min(100f, solarCharge + num2);
			if (solarCharge >= 100f && ResurrectionUtility.TryResurrect(corpse.InnerPawn, (ResurrectionParams)null))
			{
				solarCharge = 100f;
				solarCooldownTicks = 0;
				isExhausted = false;
				RemoveExhaustionHediff();
			}
		}
	}
}


