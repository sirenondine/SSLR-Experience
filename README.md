# SSLR-Experience

Synthesis patcher for [Experience](https://www.nexusmods.com/skyrimspecialedition/mods/17751) (3.x) tuned for
[Static Skill Leveling Rewritten](https://www.nexusmods.com/skyrimspecialedition/mods/89940) (SSLR).

Based on [Arkay's Commandment - Requiem-Experience](https://github.com/bondpp7/Arkays-Commandment-Requiem-Experience),
itself an extension of Azraerl's Requiem-Experience.

## What changed from the base

* **SSLR skill caps.** SSLR reads its per-skill caps from Experience (`cap = fSkillCapBase + fSkillCapMult x level`, plus racial
  bonus). The base presets pinned caps flat at 100, which disables SSLR's cap progression. Caps are now patcher settings
  (default 18 + 2/level, racial on: Experience's own defaults).
* **Experience 3.x INI.** Presets use 3.x key names (`iXPQuestObjectives`, `fKillingMult`, `fLevelFactor`, `MilitaryCamp`, ...).
  Instead of overwriting `Experience.ini`, the patcher merges only the keys it owns into the installed file and removes 2.x
  keys left behind by older presets.
* **SkyPatcher NPC levels.** Kill XP is averaged from NPC levels. Load orders that apply Requiem levels at runtime through
  SkyPatcher (`SKSE/Plugins/SkyPatcher/npc/**/*.ini`, `filterByNpcs=...:level=N`) now use those levels instead of the
  vanilla plugin values.
* **Curated actor XP kept.** Generated unique-actor XP goes to `Actors/SSLR_Synthesis.ini`; the base wrote `Actors/Default.ini`,
  replacing Experience's hand-curated list. NPCs already listed in any other actor file are skipped.
* **Skill books** are detected through Mutagen's `BookSkill` teach target (the base's flag-bit check matches nothing on current Mutagen).
* `fXPLevelUpBase`/`fXPLevelUpMult` override the vanilla GMSTs instead of adding duplicate records.
* .NET 10, Mutagen 0.54.4, Synthesis 0.36.6; no Windows-only paths.

## Settings

* **General**: quest patching, level-up curve (default 100/50; vanilla is 75/25), Experience preset (`None`, `TrueUnlevelled`, `ExtraRewarding`).
* **Skill caps (read by SSLR)**: `fSkillCapBase`, `fSkillCapMult`, `bUseRacialCaps`, optional `iMaxPlayerLevel`.
* **Skills**: suppress use-based skill gains (AVIF `UseMult = 0`, `bEnableSkillXP = false`), strip skill books (value x2),
  and what to do with "skills improve X% faster/slower" effects (Rested, Lover's Comfort, perks using the `ModSkillUse`
  entry point), which do nothing under SSLR:
  * `BonusXP` (default): each perk whose multiplier applies to all skills also grants a hidden ability that scales
    character XP gains by the same multiplier, penalties included. A controller quest (`Papyrus/Source`) polls player XP via
    SKSE only while such an effect is active. Descriptions are reworded to experience. Skill-specific ones fall back to `StripText`.
  * `StripText`: remove the skill-rate sentence from effect, perk and spell descriptions.
  * `Leave`: change nothing.

  Trainers are left untouched: training raises skill levels directly.
* **Killing XP**: SkyPatcher levels toggle, averaging mode, level-to-XP formula, plus `RaceSettings.json` (Ignore / Unique / Groups / Override).

Quest retyping rules live in `QuestSettings.json` (unchanged from the base).

## Papyrus scripts

`Papyrus/Source/*.psc` compile against vanilla sources plus SKSE's `Game.psc`; the `.pex` files in `Papyrus/Scripts` are
embedded in the patcher and written to `Data/Scripts` when `BonusXP` creates records. Recompile after editing a source.

## Installing

Add as a Git patcher in Synthesis, pointing at this repository and the `SSLR-Experience` project.

Before the first run, delete stale Experience outputs from older patchers, otherwise they shadow the new files:
`SKSE/Plugins/Experience.ini` and `SKSE/Plugins/Experience/{Races,Actors}/*` in your Synthesis output mod or MO2 overwrite.
Keep the files shipped by the Experience mod itself.

## Dry run on Linux

`tools/stage_mo2_data.py` stages a merged copy of an MO2 profile's data folder in `/tmp/sslr-run/Data`, then:

```
dotnet build -c Release SSLR-Experience
dotnet SSLR-Experience/bin/Release/net10.0/SSLR-Experience.dll run-patcher \
  --DataFolderPath /tmp/sslr-run/Data --GameRelease SkyrimSE \
  --LoadOrderFilePath /tmp/sslr-run/plugins.txt \
  --OutputPath /tmp/sslr-run/out/SSLR_Experience_Patch.esp \
  --ExtraDataFolder SSLR-Experience/Data
```
