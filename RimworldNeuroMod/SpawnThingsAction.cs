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
        public ThingDef DefToSpawn = null!;
        public int Count = 1;
    }

    public class SpawnThingsAction : NeuroAction<SpawnThingData>
    {
        public override string Name => "spawn_item";
        protected override string Description => "Spawns a specific item, resource, or building from a curated list of common things.";

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
                        ["thing_def_name"] = QJS.Enum(CommonDefs.GetCommonItemDefNames()),
                        ["count"] = new JsonSchema { Type = JsonSchemaType.Integer, Minimum = 1, Maximum = 100 }
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
            if (string.IsNullOrEmpty(defName))
            {
                return ExecutionResult.Failure("Missing required parameter 'thing_def_name'.");
            }

            var defToSpawn = DefDatabase<ThingDef>.GetNamed(defName, errorOnFail: false);
            if (defToSpawn == null)
            {
                return ExecutionResult.Failure($"Invalid thing_def_name: '{defName}'. This item cannot be spawned.");
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
                        spawnedCount++;
                    }
                    else { Log.Warning($"[Neuro] Could not find valid spot for item {i + 1}/{count}."); break; }
                }
                Log.Message($"[Neuro] Finished spawning {spawnedCount}/{count} of {thingDef.label}.");
            }
        }
    }
}
