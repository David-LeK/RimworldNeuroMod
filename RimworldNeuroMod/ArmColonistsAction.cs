#nullable enable

using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace NeuroPlaysRimworld
{
    public class ArmColonistsData { }

    public class ArmColonistsAction : NeuroAction<ArmColonistsData>
    {
        public override string Name => "arm_colonists";

        protected override string Description => "Arms all capable, unarmed colonists with the best available weapons on the map.";

        protected override JsonSchema Schema => new JsonSchema { Type = JsonSchemaType.Object };

        protected override ExecutionResult Validate(ActionJData actionData, out ArmColonistsData? parsedData)
        {
            parsedData = null;
            var map = Find.CurrentMap;
            if (map == null)
            {
                return ExecutionResult.Failure("No map loaded.");
            }

            var capableColonists = map.mapPawns.FreeColonists
                .Where(p => !p.Downed && !p.WorkTagIsDisabled(WorkTags.Violent))
                .ToList();

            if (!capableColonists.Any())
            {
                return ExecutionResult.Failure("No colonists are capable of fighting.");
            }

            parsedData = new ArmColonistsData();
            return ExecutionResult.Success("Queuing arm colonists action.");
        }

        protected override UniTask ExecuteAsync(ArmColonistsData? data)
        {
            if (data == null) return UniTask.CompletedTask;

            var map = Find.CurrentMap;
            if (map == null)
            {
                Log.Error("[Neuro] No map loaded.");
                return UniTask.CompletedTask;
            }

            var unarmedPawns = map.mapPawns.FreeColonists
                .Where(p =>
                    p.drafter != null &&
                    !p.Downed &&
                    !p.InMentalState &&
                    !p.WorkTagIsDisabled(WorkTags.Violent) &&
                    p.equipment.Primary == null)
                .ToList();

            if (!unarmedPawns.Any())
            {
                Context.Send("All capable colonists are already armed.", silent: true);
                Log.Message("[Neuro] No unarmed colonists found.");
                return UniTask.CompletedTask;
            }

            var availableWeapons = map.listerThings.AllThings
                .Where(w =>
                    (w.def.IsMeleeWeapon || w.def.IsRangedWeapon) &&
                    !w.IsBurning() &&
                    w.TryGetComp<CompEquippable>() != null)
                .OrderByDescending(w => w.GetStatValue(StatDefOf.MarketValue))
                .ToList();

            int armedCount = 0;

            foreach (var pawn in unarmedPawns)
            {
                for (int i = 0; i < availableWeapons.Count; i++)
                {
                    var weapon = availableWeapons[i];

                    if (pawn.CanReach(weapon, PathEndMode.OnCell, Danger.Deadly))
                    {
                        var job = JobMaker.MakeJob(JobDefOf.Equip, weapon);

                        // This flag allows the pawn to equip items that are "forbidden".
                        job.ignoreForbidden = true;

                        if (pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc))
                        {
                            armedCount++;
                            Log.Message($"[Neuro] Ordered {pawn.Name.ToStringShort} to equip {weapon.LabelShort}.");
                            availableWeapons.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            if (armedCount > 0)
            {
                Log.Message($"[Neuro] Tasked {armedCount} colonists to equip weapons.");
                Context.Send($"Arming {armedCount} colonists with the best available weapons.", silent: false);
            }
            else
            {
                Log.Message("[Neuro] No available weapons could be assigned.");
                Context.Send("Found unarmed colonists, but couldn't find any reachable weapons for them.", silent: true);
            }

            return UniTask.CompletedTask;
        }
    }
}
