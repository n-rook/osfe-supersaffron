using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SuperSaffron
{
    [HarmonyPatch(typeof(BossSaffron))]
    [HarmonyPatch("Start")]
    public class SaffronStart
    {
        /*
         * For extra spells, the criteria are:
         * Danger
         * Surprising targeting
         * Variety
         * Not too unfair (like... not Sunshine obviously)
         * 
         * Note that it should be possible to tag spells as "SaffronBoss" and they'll show up that way.
         * 
         * Current spells are:
         * Blackout 
         * BladeRain
         * Blast Crystals
         * Blink
         * Bow Snipe (nerfed)
         * Cryokinesis
         * Focus
         * Glitterbomb
         * Hellfire
         * Kinetic Wave
         * Magic Claw
         * Step Slash
         * Wildfire
         * 
         * Extra spells I'd like to add:
         * 
         * Amalgam
         * Blizzard (might be too much)
         * Excalibur (Stretch)
         * Firewall
         * Laser Turret
         * Miss-Me Shield
         * Thunderstorm (might be too much) (doesn't work right now)
         * Ion Cannon
         * Poison Tails
         * Rock Tomb (this can be dodged, right?)
         * Showdown
         * Sweeper
         * Tile Fire
         * Zenith / Step Slash
         */

        /*
         * Spells I've tried and don't feel right:
         * Energizer -- in practice just a boring shot
         * Monument  -- too big, too restrictive when it falls
         */
        
        /*
         * Spells that don't work at the moment:
         * 
         * Rock Tomb
         * Thunderstorm
         */

        // If true, override the randomly selected spell list
        private static readonly bool USE_TESTING_SPELL_LIST = false;
        private static readonly IList<string> TESTING_SPELL_LIST = new List<string>
        {
            "Energizer",
            "SumMonument",
            "Sweeper",
            "TileFire",
            "SumTurretlaser",
            "Showdown",
            "Caltrops"
        }.AsReadOnly();

        // These are hardcoded so they don't interfere with other mods.
        // In theory I can just add SaffronBoss to these spells' tags.
        // But in practice this means the mod will conflict with other mods that change them.
        private static readonly IList<string> EXTRA_SPELLS = new List<string>
        {
            "MissMeShield",
            "Showdown",
        }.AsReadOnly();

        private static readonly IList<string> HARD_SPELLS = new List<string>
        {
            "Amalgam",
            "Firewall",
            "IonCannon",
            "PoisonTail",
            "Sweeper",
            "TileFire",
            "Caltrops"
        }.AsReadOnly();

        private static readonly ISet<string> SPELLS_TO_NERF = new HashSet<string> {
            "Ragnarok",
            "BowSnipe"
        };

        static bool Prepare(Boss __instance)
        {
            Debug.Log("Loaded Super Saffron!");
            return true;
        }

        static bool DoAllExpectedSpellsExist(BossSaffron boss)
        {
            bool ok = true;
            foreach (string spellId in TESTING_SPELL_LIST.Concat(EXTRA_SPELLS).Concat(HARD_SPELLS))
            {
                if (!boss.ctrl.itemMan.spellDictionary.ContainsKey(spellId))
                {
                    Debug.LogWarning($"Spell ID {spellId} does not correspond to a real spell.");
                    ok = false;
                }
            }

            return ok;
        }

        private static float nerfValue(int tier)
        {
            // Change in behavior.
            // Stop nerfing Saffron's most dangerous spells at high boss tiers.
            if (tier < 3)
            {
                return 0.5f;
            } else if (tier < 5)
            {
                return 0.75f;
            } else
            {
                return 1.0f;
            }
        }

        private static void nerfSpell(SpellObject spell, BossSaffron boss)
        {
            if (!SPELLS_TO_NERF.Contains(spell.itemID))
            {
                return;
            }

            spell.damage *= nerfValue(boss.tier);
        }

        private static IEnumerable<string> ChooseSpells(BossSaffron boss)
        {
            if (USE_TESTING_SPELL_LIST)
            {
                return TESTING_SPELL_LIST;
            }

            IEnumerable<string> newSpellsForMod = EXTRA_SPELLS;
            if (boss.tier > 2)
            {
                Debug.Log("Adding 'hard spells' to Saffron's possible spells.");
                newSpellsForMod = newSpellsForMod.Concat(HARD_SPELLS);
            }

            List<string> spells = boss.ctrl.itemMan.saffronBossSpellList.Select(s => s.itemID)
                .Concat(newSpellsForMod)
                .Distinct()  // Proceed sensibly if someone else adds SaffronBoss tags to a spell we added
                .ToList();

            // This - 1 thing is copied from the base code, but I'm not sure it actually makes any sense
            List<int> intList = Utils.RandomList(spells.Count - 1, false, true);

            return intList
                .Take(5 + boss.tier)
                .Select(i => spells[i])
                .ToList();
        }

        // Load spells
        // Does not modify boss.randSpells
        private static List<SpellObject> LoadSpells(BossSaffron boss)
        {
            List<SpellObject> spells = ChooseSpells(boss)
                .Select((spellId) => boss.ctrl.deCtrl.CreateSpellBase(spellId, (Being)boss, true))
                .ToList();
            foreach (SpellObject spell in spells)
            {
                nerfSpell(spell, boss);
            }
            return spells;
        }

        private static void ReplacementLoadSpells(BossSaffron boss)
        {
            Debug.Log("Reloading spells for SuperSaffron.");
            if (!DoAllExpectedSpellsExist(boss))
            {
                Debug.LogWarning("Ran into trouble. SuperSaffron doing nothing.");
                return;
            }

            List<SpellObject> newSpells = LoadSpells(boss);
            boss.randSpells.Clear();
            boss.randSpells.AddRange(newSpells);

            string spellIds = boss.randSpells.Select(s => s.itemID).Join();
            Debug.Log($"Successfully reloaded spells for SuperSaffron: {spellIds}");
        }

        [HarmonyPostfix]
        static void Postfix(BossSaffron __instance)
        {
            ReplacementLoadSpells(__instance);
        }
    }
}
