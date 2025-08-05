#nullable enable

using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace NeuroPlaysRimworld
{
    public static class CommonDefs
    {
        public static readonly List<ThingDef> CommonItems = new List<ThingDef>();

        public static readonly List<PawnKindDef> CommonPawns = new List<PawnKindDef>();

        static CommonDefs()
        {
            void AddThingDef(string defName)
            {
                var def = DefDatabase<ThingDef>.GetNamed(defName, errorOnFail: false);
                if (def != null) CommonItems.Add(def);
            }

            void AddPawnKindDef(string defName)
            {
                var def = DefDatabase<PawnKindDef>.GetNamed(defName, errorOnFail: false);
                if (def != null) CommonPawns.Add(def);
            }

            // --- Common Items ---
            // Food
            AddThingDef("MealSimple");
            AddThingDef("MealFine");
            AddThingDef("MealSurvivalPack");
            AddThingDef("Kibble");

            // Resources
            AddThingDef("WoodLog");
            AddThingDef("Steel");
            AddThingDef("Plasteel");
            AddThingDef("ComponentIndustrial");
            AddThingDef("ComponentSpacer");

            // Medicine
            AddThingDef("MedicineHerbal");
            AddThingDef("MedicineIndustrial");
            AddThingDef("MedicineUltratech");

            // Drugs
            AddThingDef("Beer");
            AddThingDef("PsychiteTea");
            AddThingDef("GoJuice");

            // Weapons
            AddThingDef("Gun_AssaultRifle");
            AddThingDef("Gun_ChargeRifle");
            AddThingDef("Gun_SniperRifle");
            AddThingDef("MeleeWeapon_MonoSword");

            // --- Common Pawns ---
            // Animals
            AddPawnKindDef("Muffalo");
            AddPawnKindDef("Alpaca");
            AddPawnKindDef("Husky");
            AddPawnKindDef("Warg");
            AddPawnKindDef("Thrumbo");

            // Humans
            AddPawnKindDef("Colonist");
            AddPawnKindDef("Villager");
            AddPawnKindDef("Pirate");

            // Mechs
            AddPawnKindDef("Mech_Scyther");
            AddPawnKindDef("Mech_Lancer");
            AddPawnKindDef("Mech_Centipede");
        }

        public static IEnumerable<string> GetCommonItemDefNames() => CommonItems.Select(d => d.defName).OrderBy(n => n);
        public static IEnumerable<string> GetCommonPawnDefNames() => CommonPawns.Select(d => d.defName).OrderBy(n => n);
    }
}