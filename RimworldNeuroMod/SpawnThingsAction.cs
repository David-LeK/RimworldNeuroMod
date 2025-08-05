#nullable enable

using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace NeuroPlaysRimworld
{
    public class SpawnThingData
    {
        public ThingDef DefToSpawn;
        public int Count = 1;
    }

    public class SpawnThingsAction : NeuroAction<SpawnThingData>
    {
        private static readonly Dictionary<string, ThingDef> AllSpawnableThings = new Dictionary<string, ThingDef>();

        static SpawnThingsAction()
        {
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.category == ThingCategory.Ethereal || !def.PlayerAcquirable || def.isUnfinishedThing || def.IsCorpse || def.IsFrame || def.IsBlueprint || def.defName.Contains("Unfinished")) continue;
                if (def.category != ThingCategory.Pawn && def.category != ThingCategory.None)
                {
                    AllSpawnableThings[def.defName] = def;
                }
            }
        }

        public SpawnThingsAction() { }

        public override string Name => "spawn_item";
        protected override string Description => "Spawns a specific item, resource, or building from a list of all available things.";

        protected override JsonSchema Schema
        {
            get
            {
                return new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "thing_def_name" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["thing_def_name"] = QJS.Enum(AllSpawnableThings.Keys.OrderBy(k => k)),
                        ["count"] = new JsonSchema { Type = JsonSchemaType.Integer, Minimum = 1, Maximum = 5 }
                    }
                };
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out SpawnThingData? parsedData)
        {
            parsedData = null;
            var dataObject = actionData.Data as JObject;
            if (dataObject == null) return ExecutionResult.Failure("Invalid parameters.");

            var defName = dataObject["thing_def_name"]?.Value<string>();
            if (string.IsNullOrEmpty(defName) || !AllSpawnableThings.TryGetValue(defName, out var defToSpawn))
            {
                return ExecutionResult.Failure($"Invalid thing_def_name: '{defName}'. Please choose from the provided list.");
            }

            parsedData = new SpawnThingData
            {
                DefToSpawn = defToSpawn,
                Count = dataObject["count"]?.Value<int?>() ?? 1
            };
            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync(SpawnThingData? data)
        {
            if (data != null)
            {
                var map = Find.CurrentMap;
                if (map == null) Log.Error("[Neuro] SpawnItemAction failed: No map loaded.");
                else SpawnItems(map, data.DefToSpawn, data.Count);
            }
            return UniTask.CompletedTask;
        }

        private void SpawnItems(Map map, ThingDef thingDef, int count)
        {
            Predicate<IntVec3> validator = c =>
              (thingDef.category == ThingCategory.Building || thingDef.category == ThingCategory.Plant ?
                GenConstruct.CanPlaceBlueprintAt(thingDef, c, Rot4.North, map, false, null).Accepted :
                c.Standable(map) && !c.GetThingList(map).Any(t => t.def.IsEdifice())) &&
              !map.fogGrid.IsFogged(c) &&
              map.reachability.CanReachColony(c);

            if (thingDef.stackLimit > 1)
            {
                if (CellFinder.TryFindRandomCellNear(map.Center, map, 25, validator, out var spawnPos))
                {
                    Thing newThing = ThingMaker.MakeThing(thingDef, GenStuff.DefaultStuffFor(thingDef));
                    newThing.stackCount = count;
                    GenPlace.TryPlaceThing(newThing, spawnPos, map, ThingPlaceMode.Direct);
                    Log.Message($"[Neuro] Spawned {count}x {thingDef.label} at ({spawnPos.x}, {spawnPos.z}).");
                }
                else { Log.Warning($"[Neuro] Could not find a valid placement spot for {thingDef.label}."); }
            }
            else
            {
                int spawnedCount = 0;
                for (int i = 0; i < count; i++)
                {
                    if (CellFinder.TryFindRandomCellNear(map.Center, map, 25, validator, out var spawnPos))
                    {
                        GenPlace.TryPlaceThing(ThingMaker.MakeThing(thingDef, GenStuff.DefaultStuffFor(thingDef)), spawnPos, map, ThingPlaceMode.Direct);
                        Log.Message($"[Neuro] Spawned 1x {thingDef.label} at ({spawnPos.x}, {spawnPos.z}).");
                        spawnedCount++;
                    }
                    else { Log.Warning($"[Neuro] Could not find valid spot for item {i + 1}/{count}."); break; }
                }
                Log.Message($"[Neuro] Finished spawning {spawnedCount}/{count} of {thingDef.label}.");
            }
        }
    }
}