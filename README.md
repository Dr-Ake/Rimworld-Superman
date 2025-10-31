# 🦸‍♂️ RimWorld: Kryptonian Gene Mod — Complete Technical Design Document

## 🧬 Overview
This mod adds a **Kryptonian Gene Injector** that grants a pawn powers similar to Superman.  
The system is built entirely using **vanilla RimWorld APIs** (no custom assets or dependencies).  
All powers, invulnerability, solar logic, and resurrection are handled by a single C# component:  
`CompKryptonianSolar`.

---

## ⚙️ Core Component: `CompKryptonianSolar`

### Primary Variables
```csharp
float SolarCharge;            // 0–100, represents solar energy percent
int solarCooldownTicks;       // ticks remaining before recharge resumes
bool isFlying;                // true when Flight mode is active
bool showingPowersMenu;       // true when in main Powers submenu
bool showingMiscMenu;         // true when in Misc submenu
bool isExhausted;             // true when SolarCharge == 0
```

---

## ☀️ Solar Energy System

### Recharge
- SolarCharge increases passively during daylight using:
  ```csharp
  float sunlight = GenCelestial.CurCelestialSunGlow(map);
  ```
- Recharge rate = `sunlight * 0.05f` per tick.
  - At noon (1.0 SunGlow): +0.05% per tick.
  - At dawn/dusk (~0.5): +0.025% per tick.
- Recharge **does not occur** if:
  - Pawn is under a roof.
  - Pawn is in Solar Exhaustion cooldown.
  - SolarCharge == 100.

### Drain
- Every active power consumes SolarCharge based on use.
- Damage absorption also reduces SolarCharge.

### Cooldown
- When SolarCharge reaches 0, Superman enters **Solar Exhaustion**.
- Cooldown = **30,000 ticks (≈12 in-game hours)**.
- During this time, **no recharge** occurs, even in sunlight.

---

## 💀 Resurrection System

### Core Rules
- Superman **cannot truly die** if any SolarCharge remains.
- While SolarCharge > 0:
  - Damage that would kill him instead drains remaining SolarCharge.
  - Once SolarCharge = 0, death is allowed normally.
- However, even if dead, he can **resurrect through sunlight exposure.**

### Resurrection Logic

#### Conditions that *enable* resurrection:
1. Corpse is **not under a roof**.
2. Corpse is **reachable by sunlight** (`GenCelestial.CurCelestialSunGlow(map) > 0`).
3. Corpse is **not in a cryptosleep pod or fully buried under terrain.**
4. Corpse may be in a **coffin**, as long as the coffin itself is exposed to the sun (unroofed, outdoors).

#### Behavior:
- While corpse is exposed to sunlight:
  - SolarCharge regenerates at normal daytime rate.
  - At SolarCharge == 100:
    - Trigger `ResurrectionUtility.TryResurrect(corpse.InnerPawn)`
    - Reset SolarCharge to 100, clear SolarExhaustion.
- If corpse is **indoors, roofed, or buried**, no SolarCharge gain occurs.
- The body can be **moved into sunlight** later — resurrection begins immediately once exposed.

#### Fire, Decay, and Rot:
- While corpse belongs to a pawn with this comp:
  - Immune to fire damage.
  - Does not rot, decay, or deteriorate.
  - Displays a custom “Dormant” state instead of dead rot graphic.

---

## 💪 Super Strength (Passive Ability)

| Effect | Description | Multiplier |
|---------|--------------|-------------|
| Melee DPS | Increased damage output | ×4 |
| Carrying Capacity | Increased mass haulable | ×3 |
| Mining Speed | Faster resource breaking | ×3 |
| Move Speed | Faster walking/running | ×1.5 |

- Strength is **constant** as long as SolarCharge > 0.
- When SolarCharge == 0, all stats revert to 50% of normal (Solar Exhaustion penalty).

---

## 🛡️ Damage Reduction / Invulnerability

### Flat Reduction
- Any incoming damage **below 80** is completely ignored.  
  (e.g., pistol bullets, knives, fists — no effect.)
- If incoming damage ≥ 80:
  - Superman takes **(damage - 80)**.
  - Each hit reduces SolarCharge by `(damage * 0.05f)` (capped at 10% loss per hit).

### Special Rules
- Fire, Heat, and Explosion damage are **ignored entirely**.
- Only heavy ordnance (explosives, doomsday rockets, orbital strikes, mech bosses) can realistically harm him.

---

## 🧠 Power Management

Superman’s powers are accessed through a **nested gizmo menu** using vanilla RimWorld `Command_Action` and `Command_Toggle`.

### Menu Structure
```
[Powers]
 ├─ Heat Vision ▶
 │    ├─ Low
 │    ├─ Medium
 │    ├─ High
 │    └─ Lethal
 ├─ Ice Breath
 ├─ Flight (toggle)
 ├─ Miscellaneous ▶
 │    ├─ Cellophane S
 │    ├─ X-Ray Vision
 │    ├─ Supernova
 │    └─ ← Back
 └─ ← Back
```

---

## 🔥 Heat Vision

### Modes
| Mode | Damage | Explosion Radius | Fire Chance | Solar Drain |
|-------|---------|------------------|--------------|--------------|
| Low | 10 | 1 tile | 0.2 | 0.1% per tick |
| Medium | 25 | 2 tiles | 0.5 | 0.3% per tick |
| High | 60 | 3 tiles | 0.8 | 0.7% per tick |
| Lethal | 120 | 4 tiles | 1.0 | 1.5% per tick |

### Function
- Clicking mode activates targeting reticle.
- Clicking a tile triggers:
  ```csharp
  GenExplosion.DoExplosion(targetCell, map, radius, DamageDefOf.Flame, Pawn, damage, armorPenetration: 1f);
  ```
- Holding the button or clicking repeatedly simulates continuous firing.
- Heat Vision **starts fires** based on Fire Chance column.
- Fire damage is real; can ignite terrain and pawns.

### Visuals
- Red glow motes and sparks along beam.
- Fire/sizzle sound on contact (`EnergyShield_Broken` or `BulletImpact_Flame`).
- No light/glow on Superman himself.

---

## ❄️ Ice Breath

- Cone AoE, 90° forward, length: 7 tiles.
- Each tile hit:
  - `GenTemperature.PushHeat(cell, map, -30f);`
  - Extinguish any fire.
  - Stun pawns for 180 ticks.
  - Add minor Hypothermia (stage 1).
- SolarDrain = 0.5% per use.
- Visual: white-blue smoke motes, cold wind sound.
- Fire immunity unaffected — his own breath won’t harm him.

---

## 🪶 Flight Mode

### Toggle Behavior
- Activated through gizmo “Flight”.
- While active:
  - Pawn ignores terrain costs and obstacles (`PathGrid.Walkable = true` for him).
  - Movement speed = +50%.
  - Uses air pathing (like Mech drop pods).
  - Can cross water and mountain tiles.
- SolarDrain = 0.05% per tick while active.
- Auto-disables if SolarCharge == 0.
- No glow, aura, or trail — visually he moves normally.

---

## 🩸 Cellophane S (Misc Power)

- Targeted ability.
- On activation:
  - `targetPawn.stances.stunner.StunFor(300, userPawn);`
  - Applies visual effect (red smoke motes forming a “wrap”).
- Duration: 5 seconds.
- SolarDrain = 2%.
- Range: 10 tiles.

---

## 👁 X-Ray Vision

### Effect
- When activated:
  - All fog-of-war within 30 tiles of Superman is permanently cleared:
    ```csharp
    foreach (IntVec3 c in GenRadial.RadialCellsAround(Pawn.Position, 30, true))
        map.fogGrid.Unfog(c);
    ```
- **No timer.** The revealed area stays visible permanently.
- Reveals inside mountains, buildings, and enclosed rooms.
- Does not drain continuously — single activation cost.
- SolarDrain = 1%.

---

## 🌞 Supernova (Catastrophic Power)

### Activation
- Access via Misc → Supernova.
- Clicking activates a radius selection drag.
- Player drags outward to set explosion radius (min 10, max 120 tiles).
- Prompt:  
  *“Release all solar energy? This will annihilate everything in the chosen radius.”*
- On confirm, trigger the event.

### Effect
- Damage = 500 Bomb per tile in radius.
- Explosion stops at first solid obstacle (stone, uranium, plasteel, etc.).
- Anything hit directly is destroyed.
- Uranium may cause chain micro-explosions (20% chance):
  ```csharp
  if (thing.def == ThingDefOf.Uranium && Rand.Chance(0.2f))
      GenExplosion.DoExplosion(thing.Position, map, 5f, DamageDefOf.Flame, Pawn, 300);
  ```
- SolarCharge set to 0 (always full drain).
- Applies `SolarExhaustion` hediff.
- Pawn stunned for 600 ticks.

### Aftermath Zones
| Zone | Range | Description |
|-------|--------|--------------|
| **Core (0–60%)** | Instantly deletes all Things. Terrain replaced with BurnedGround + Filth_Ash. No fires here. |
| **Rim (60–100%)** | Fires ignite on all tiles, smoke motes, +2000°C heat. |
| **Outer (>100%)** | PushHeat to simulate shockwave and fire spread. |

### Visual Sequence
1. Massive white flash (`ThrowLightningGlow`)
2. 0.5s delay → `PsychicPulseGlobal` sound
3. Fire/smoke wave spreading outward (chain explosions)
4. Screen shake if possible
5. Superman collapses (SolarExhaustion)

### Result
- Map-wide destruction for large radius.
- Only plasteel or deep mountain may survive the blast.
- Superman enters 12-hour cooldown with SolarCharge = 0.

---

## ☀️ Fire, Decay, and Heat Immunity

Applies globally while Kryptonian gene is active:
- Fire damage: ignored completely.
- Heat damage: ignored.
- Burnable flag removed from pawn.
- Corpse: cannot ignite or decay.

---

## 🧩 Implementation Overview

**Files**
- `CompKryptonianSolar.cs` — handles all logic and gizmo creation.
- `ThingDef_KryptonianGene.xml` — defines the injector item.
- Optional `HediffDefs.xml` for `SolarExhaustion` status.

**Harmony Hooks**
| Hook | Purpose |
|------|----------|
| `PostApplyDamage` | Damage negation and SolarCharge drain |
| `TickRare` | Solar recharge + cooldown management |
| `Resurrect` | Custom sunlight-based resurrection logic |
| `Pawn.GetGizmos` | Adds power menus |

**Dependencies:** None.  
Compatible with Core + all DLCs.

---

## 🕹️ Example Gameplay Loop

1. Player crafts **Kryptonian Gene Injector**.  
2. Pawn uses injector, gaining gene and full SolarCharge.  
3. Powers gizmo appears.  
4. Player uses powers freely; SolarCharge slowly drains.  
5. If SolarCharge = 0 → Superman collapses, enters Solar Exhaustion (12h cooldown).  
6. If killed outdoors → corpse slowly recharges via sunlight, resurrects automatically at 100%.  
7. If buried indoors or roofed → remains inert until moved into sunlight.  
8. Player may unleash **Supernova** to destroy everything — resetting charge to 0 and entering exhaustion.

---

## ✅ Design Summary

- **Full Power until Empty:** Superman has complete strength and ability as long as SolarCharge > 0.  
- **Two States Only:** Powered (≥1%) or Drained (0%).  
- **Sunlight Is Life:** everything — strength, survival, and resurrection — depends on sunlight.  
- **No Glow:** Superman never visually glows or emits light.  
- **All Vanilla Systems:** uses RimWorld’s built-in damage, explosion, and map logic.  
- **Self-contained:** one comp handles all logic; future expansions can add Kryptonite, Red Sun debuffs, or AI behavior.
