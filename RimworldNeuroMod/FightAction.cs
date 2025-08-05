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
using System.Text.RegularExpressions;
using Verse;
using Verse.AI;

namespace NeuroPlaysRimworld
{
    public class FightData
    {
        public Pawn Colonist = null!;
        public Thing Target = null!;
    }

    public class FightAction : NeuroAction<FightData>
    {
        public override string Name => "fight";

        protected override string Description => "Orders a colonist to attack a specific enemy or animal on the map.";

        private static string GetTargetDisplayName(Thing target)
        {
            return $"{target.LabelShortCap} (ID: {target.ThingID})";
        }

        protected override JsonSchema Schema
        {
            get
            {
                var map = Find.CurrentMap;
                if (map == null) return new JsonSchema();

                var capableColonists = map.mapPawns.FreeColonists
                    .Where(p => !p.Downed && !p.WorkTagIsDisabled(WorkTags.Violent))
                    .Select(p => p.Name.ToStringShort);

                var centerPoint = map.mapPawns.FreeColonists.Any() ? map.mapPawns.FreeColonists.RandomElement().Position : map.Center;
                const float relevantRadius = 100f;

                var hostileTargets = map.attackTargetsCache.TargetsHostileToColony
                    .Select(t => t.Thing)
                    .Where(t => t is Pawn p && !p.Downed && t.Position.InHorDistOf(centerPoint, relevantRadius))
                    .ToList();

                var wildAnimals = map.mapPawns.AllPawnsSpawned
                    .Where(p => p.RaceProps.Animal && p.Faction == null && !p.Downed && p.Position.InHorDistOf(centerPoint, relevantRadius))
                    .ToList();

                var allTargets = hostileTargets
                    .Concat(wildAnimals)
                    .Distinct()
                    .OrderBy(t => t.LabelCap)
                    .Take(15)
                    .Select(GetTargetDisplayName);

                return new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "colonist_name", "target_name" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["colonist_name"] = QJS.Enum(capableColonists),
                        ["target_name"] = QJS.Enum(allTargets)
                    }
                };
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out FightData? parsedData)
        {
            parsedData = null;
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failure("No map loaded.");

            var colonistName = actionData.Data?["colonist_name"]?.Value<string>();
            var targetDisplayName = actionData.Data?["target_name"]?.Value<string>();

            if (string.IsNullOrEmpty(colonistName) || string.IsNullOrEmpty(targetDisplayName))
            {
                return ExecutionResult.Failure("Missing required parameters: 'colonist_name' or 'target_name'.");
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
            if (pawn.InMentalState)
            {
                return ExecutionResult.Failure($"{colonistName} is having a mental break and cannot be ordered to fight.");
            }

            var match = Regex.Match(targetDisplayName, @"\(ID: ([^)]+)\)$");
            if (!match.Success)
            {
                return ExecutionResult.Failure($"Invalid target format: '{targetDisplayName}'.");
            }
            string targetId = match.Groups[1].Value;

            var target = map.listerThings.AllThings.FirstOrDefault(t => t.ThingID == targetId);

            if (target == null || target.Destroyed || (target is Pawn p && (p.Dead || p.Downed)))
            {
                return ExecutionResult.Failure($"Target '{targetDisplayName}' is no longer available.");
            }

            if (!(target is Pawn))
            {
                return ExecutionResult.Failure($"Target '{targetDisplayName}' is not a valid attack target (not a pawn).");
            }

            if (!pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly))
            {
                return ExecutionResult.Failure($"{colonistName} cannot reach the target '{targetDisplayName}'.");
            }

            parsedData = new FightData { Colonist = pawn, Target = target };
            return ExecutionResult.Success($"Queuing order for {colonistName} to attack {target.LabelShort}.");
        }

        protected override UniTask ExecuteAsync(FightData? data)
        {
            if (data == null) return UniTask.CompletedTask;

            var pawn = data.Colonist;
            var target = data.Target;

            if (pawn.drafter != null && !pawn.drafter.Drafted)
            {
                pawn.drafter.Drafted = true;
            }

            JobDef attackJobDef = (pawn.equipment?.Primary?.def.IsRangedWeapon ?? false)
                ? JobDefOf.AttackStatic
                : JobDefOf.AttackMelee;

            var job = JobMaker.MakeJob(attackJobDef, target);

            if (pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc))
            {
                Log.Message($"[Neuro] Ordered {pawn.Name.ToStringShort} to attack {target.LabelShort}.");
                Context.Send($"Ordering {pawn.Name.ToStringShort} to attack {target.LabelShort}!", silent: false);
            }
            else
            {
                Log.Warning($"[Neuro] Failed to order {pawn.Name.ToStringShort} to attack {target.LabelShort}.");
                Context.Send($"Could not order {pawn.Name.ToStringShort} to attack {target.LabelShort}.", silent: true);
            }

            return UniTask.CompletedTask;
        }
    }
}
