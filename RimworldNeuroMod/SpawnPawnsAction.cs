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

namespace NeuroPlaysRimworld
{
    public class SpawnPawnData
    {
        public PawnKindDef DefToSpawn;
        public int Count = 1;
        public string? Name;
    }

    public class SpawnPawnsAction : NeuroAction<SpawnPawnData>
    {
        private static readonly Dictionary<string, PawnKindDef> AllSpawnablePawns = new Dictionary<string, PawnKindDef>();

        static SpawnPawnsAction()
        {
            foreach (var def in DefDatabase<PawnKindDef>.AllDefs)
            {
                if (def.race != null && def.race.race != null && def.showInDebugSpawner && def.race.category == ThingCategory.Pawn)
                {
                    AllSpawnablePawns[def.defName] = def;
                }
            }
        }

        public SpawnPawnsAction() { }

        public override string Name => "spawn_pawn";
        protected override string Description => "Spawns a pawn, like an animal or human. Can optionally provide a custom name.";

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
                        ["pawn_kind_def_name"] = QJS.Enum(AllSpawnablePawns.Keys.OrderBy(k => k)),
                        ["count"] = new JsonSchema { Type = JsonSchemaType.Integer, Minimum = 1, Maximum = 5 },
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

            if (string.IsNullOrEmpty(defName) || !AllSpawnablePawns.TryGetValue(defName, out var defToSpawn))
            {
                return ExecutionResult.Failure($"Invalid pawn_kind_def_name: '{defName}'. Please choose from the provided list.");
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

        private void SpawnPawns(Map map, PawnKindDef pawnKindDef, int count, string? name)
        {
            Faction? faction = null;
            if (pawnKindDef.race.race.Animal) faction = null;
            else if (pawnKindDef.race.race.IsMechanoid) faction = Faction.OfMechanoids;
            else
            {
                if (!Find.FactionManager.AllFactions.Where(f => f.def == pawnKindDef.defaultFactionDef).TryRandomElement(out faction))
                {
                    faction = Faction.OfAncientsHostile;
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (!RCellFinder.TryFindRandomPawnEntryCell(out var spawnCell, map, CellFinder.EdgeRoadChance_Hostile))
                {
                    Log.Warning($"[Neuro] Could not find a valid entry cell for pawn {i + 1}/{count}.");
                    continue;
                }

                var request = new PawnGenerationRequest(pawnKindDef, faction, forceGenerateNewPawn: true);
                var pawn = PawnGenerator.GeneratePawn(request);

                if (i == 0 && !string.IsNullOrEmpty(name))
                {
                    pawn.Name = new NameSingle(name);
                }

                GenSpawn.Spawn(pawn, spawnCell, map);
                Log.Message($"[Neuro] Spawned {pawn.NameShortColored} at edge of map ({spawnCell.x}, {spawnCell.z}).");
            }
        }
    }
}