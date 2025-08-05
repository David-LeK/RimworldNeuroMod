#nullable enable

using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace NeuroPlaysRimworld
{
    public class ArmIndividuallyData
    {
        public Pawn Pawn = null!;
        public Thing Weapon = null!;
    }

    public class ArmIndividuallyAction : NeuroAction<ArmIndividuallyData>
    {
        public override string Name => "arm_individually";

        protected override string Description => "Arms a specific capable colonist with a chosen weapon available on the map.";

        protected override JsonSchema Schema
        {
            get
            {
                var map = Find.CurrentMap;
                if (map == null) return new JsonSchema();

                var capableColonists = map.mapPawns.FreeColonists
                    .Where(p => !p.Downed && !p.WorkTagIsDisabled(WorkTags.Violent))
                    .Select(p => p.Name.ToStringShort);

                var availableWeapons = map.listerThings.AllThings
                    .Where(w =>
                        (w.def.IsMeleeWeapon || w.def.IsRangedWeapon) &&
                        !w.IsBurning() &&
                        w.TryGetComp<CompEquippable>() != null)
                    .Select(w => w.LabelCap.ToString())
                    .Distinct()
                    .OrderBy(name => name);

                return new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "colonist_name", "weapon_name" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["colonist_name"] = QJS.Enum(capableColonists),
                        ["weapon_name"] = QJS.Enum(availableWeapons)
                    }
                };
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out ArmIndividuallyData? parsedData)
        {
            parsedData = null;
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failure("No map loaded.");

            var colonistName = actionData.Data?["colonist_name"]?.Value<string>();
            var weaponName = actionData.Data?["weapon_name"]?.Value<string>();

            if (string.IsNullOrEmpty(colonistName) || string.IsNullOrEmpty(weaponName))
            {
                return ExecutionResult.Failure("Missing required parameters: 'colonist_name' or 'weapon_name'.");
            }

            var pawn = map.mapPawns.FreeColonists.FirstOrDefault(p => p.Name.ToStringShort == colonistName);
            if (pawn == null)
            {
                return ExecutionResult.Failure($"Colonist '{colonistName}' not found.");
            }

            if (pawn.Downed || pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return ExecutionResult.Failure($"{colonistName} is currently unable to fight.");
            }

            // Find the best available weapon that matches the chosen name.
            var weapon = map.listerThings.AllThings
                .Where(w =>
                    w.LabelCap == weaponName &&
                    (w.def.IsMeleeWeapon || w.def.IsRangedWeapon) &&
                    !w.IsBurning() &&
                    w.TryGetComp<CompEquippable>() != null)
                .OrderByDescending(w => w.MarketValue) // If multiple match (e.g., two "Poor Short Bows"), pick the one with higher value
                .FirstOrDefault();

            if (weapon == null)
            {
                return ExecutionResult.Failure($"Weapon '{weaponName}' is no longer available on the map.");
            }

            if (!pawn.CanReach(weapon, PathEndMode.OnCell, Danger.Deadly))
            {
                return ExecutionResult.Failure($"{colonistName} cannot reach the {weaponName}.");
            }

            parsedData = new ArmIndividuallyData { Pawn = pawn, Weapon = weapon };
            return ExecutionResult.Success($"Queuing order for {colonistName} to equip {weaponName}.");
        }

        protected override UniTask ExecuteAsync(ArmIndividuallyData? data)
        {
            if (data == null) return UniTask.CompletedTask;

            var job = JobMaker.MakeJob(JobDefOf.Equip, data.Weapon);
            job.ignoreForbidden = true;

            if (data.Pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc))
            {
                Log.Message($"[Neuro] Ordered {data.Pawn.Name.ToStringShort} to equip {data.Weapon.LabelShort}.");
                Context.Send($"Ordering {data.Pawn.Name.ToStringShort} to equip {data.Weapon.LabelShort}.", silent: false);
            }
            else
            {
                Log.Warning($"[Neuro] Failed to order {data.Pawn.Name.ToStringShort} to equip {data.Weapon.LabelShort}.");
                Context.Send($"Could not order {data.Pawn.Name.ToStringShort} to equip {data.Weapon.LabelShort}.", silent: true);
            }

            return UniTask.CompletedTask;
        }
    }
}
