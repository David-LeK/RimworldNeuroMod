#nullable enable

using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace NeuroPlaysRimworld
{
    public class SpawnPawnData
    {
        public PawnKindDef DefToSpawn = null!;
        public int Count = 1;
        public string? Name;
    }

    public class SpawnPawnsAction : NeuroAction<SpawnPawnData>
    {
        public override string Name => "spawn_pawn";
        protected override string Description => "Spawns a common pawn, like an animal or human. Can optionally provide a custom name for the first pawn spawned.";

        protected override JsonSchema Schema
        {
            get
            {
                return new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "pawn_kind_def_name" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["pawn_kind_def_name"] = QJS.Enum(CommonDefs.GetCommonPawnDefNames()),
                        ["count"] = new JsonSchema { Type = JsonSchemaType.Integer, Minimum = 1, Maximum = 10 },
                        ["name"] = new JsonSchema { Type = JsonSchemaType.String }
                    }
                };
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out SpawnPawnData? parsedData)
        {
            parsedData = null;
            var dataObject = actionData.Data as JObject;
            if (dataObject == null) return ExecutionResult.Failure("Invalid parameters.");

            var defName = dataObject["pawn_kind_def_name"]?.Value<string>();

            if (string.IsNullOrEmpty(defName))
            {
                return ExecutionResult.Failure("Missing required parameter 'pawn_kind_def_name'.");
            }

            var defToSpawn = DefDatabase<PawnKindDef>.GetNamed(defName, errorOnFail: false);
            if (defToSpawn == null)
            {
                return ExecutionResult.Failure($"Invalid pawn_kind_def_name: '{defName}'. This pawn cannot be spawned.");
            }

            parsedData = new SpawnPawnData
            {
                DefToSpawn = defToSpawn,
                Count = dataObject["count"]?.Value<int?>() ?? 1,
                Name = dataObject["name"]?.Value<string>()
            };
            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync(SpawnPawnData? data)
        {
            if (data != null)
            {
                var map = Find.CurrentMap;
                if (map == null) Log.Error("[Neuro] SpawnPawnAction failed: No map loaded.");
                else SpawnPawns(map, data.DefToSpawn, data.Count, data.Name);
            }
            return UniTask.CompletedTask;
        }

        private bool TryFindSafeSpawnCell(Map map, out IntVec3 result)
        {
            if (RCellFinder.TryFindRandomPawnEntryCell(out result, map, CellFinder.EdgeRoadChance_Hostile))
            {
                return true;
            }
            if (CellFinder.TryFindRandomCellNear(map.Center, map, 20, c => c.Standable(map) && !c.Roofed(map) && map.reachability.CanReachColony(c), out result))
            {
                return true;
            }
            return CellFinder.TryFindRandomCell(map, c => c.Standable(map) && !c.Fogged(map), out result);
        }

        private void SpawnPawns(Map map, PawnKindDef pawnKindDef, int count, string? name)
        {
            Faction? faction = FactionUtility.DefaultFactionFrom(pawnKindDef.defaultFactionDef);

            for (int i = 0; i < count; i++)
            {
                if (!TryFindSafeSpawnCell(map, out var spawnCell))
                {
                    Log.Error($"[Neuro] Could not find any valid spawn cell for pawn '{pawnKindDef.defName}'. Aborting spawn.");
                    break;
                }

                var request = new PawnGenerationRequest(
                    kind: pawnKindDef,
                    faction: faction,
                    context: PawnGenerationContext.NonPlayer,
                    forceGenerateNewPawn: true,
                    allowDowned: true,
                    canGeneratePawnRelations: true
                );

                var pawn = PawnGenerator.GeneratePawn(request);
                if (pawn == null)
                {
                    Log.Error($"[Neuro] PawnGenerator failed to generate pawn for kindDef {pawnKindDef.defName}.");
                    continue;
                }

                if (i == 0 && !string.IsNullOrEmpty(name))
                {
                    pawn.Name = new NameSingle(name);
                }

                GenSpawn.Spawn(pawn, spawnCell, map);

                if (pawn.Faction != null && pawn.Faction != Faction.OfPlayer)
                {
                    var lordJob = new LordJob_DefendPoint(spawnCell);
                    LordMaker.MakeNewLord(pawn.Faction, lordJob, map, new[] { pawn });
                }

                Log.Message($"[Neuro] Spawned {pawn.NameShortColored} at ({spawnCell.x}, {spawnCell.z}).");
            }
        }
    }
}
