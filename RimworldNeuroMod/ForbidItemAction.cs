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

namespace NeuroPlaysRimworld
{
    public class ForbidItemData
    {
        public Thing Item = null!;
        public bool Forbid;
    }

    public class ForbidItemAction : NeuroAction<ForbidItemData>
    {
        public override string Name => "forbid_item";

        protected override string Description => "An action to forbid or unforbid a specific stack of items on the ground.";

        private static string GetItemDisplayName(Thing item)
        {
            // Provides a unique name for the dropdown, e.g., "Packaged survival meal x10 (ID: Thing_MealSurvivalPack_123)"
            return $"{item.LabelCap} (ID: {item.ThingID})";
        }

        protected override JsonSchema Schema
        {
            get
            {
                var map = Find.CurrentMap;
                if (map == null) return new JsonSchema();

                // Find items that can be forbidden: they must be haulable, in the "Item" category, have the right component, and not be in the fog.
                var forbiddableItems = map.listerThings.AllThings
                    .Where(t => t.def.EverHaulable && t.def.category == ThingCategory.Item && t.TryGetComp<CompForbiddable>() != null && !t.Position.Fogged(map))
                    .OrderBy(t => t.Label)
                    .Select(GetItemDisplayName);

                return new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "item_name", "forbid" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["item_name"] = QJS.Enum(forbiddableItems),
                        ["forbid"] = new JsonSchema { Type = JsonSchemaType.Boolean }
                    }
                };
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out ForbidItemData? parsedData)
        {
            parsedData = null;
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failure("No map loaded.");

            var itemName = actionData.Data?["item_name"]?.Value<string>();
            var forbid = actionData.Data?["forbid"]?.Value<bool?>();

            if (string.IsNullOrEmpty(itemName) || !forbid.HasValue)
            {
                return ExecutionResult.Failure("Missing required parameters: 'item_name' or 'forbid'.");
            }

            // Extract the ThingID from the display name
            var match = Regex.Match(itemName, @"\(ID: ([^)]+)\)$");
            if (!match.Success)
            {
                return ExecutionResult.Failure($"Invalid item format: '{itemName}'.");
            }
            string itemId = match.Groups[1].Value;

            // Find the thing using its unique ID
            var item = map.listerThings.AllThings.FirstOrDefault(t => t.ThingID == itemId);

            if (item == null || item.Destroyed)
            {
                return ExecutionResult.Failure($"Item '{itemName}' is no longer available on the map.");
            }

            var forbiddableComp = item.TryGetComp<CompForbiddable>();
            if (forbiddableComp == null)
            {
                return ExecutionResult.Failure($"Item '{itemName}' cannot be forbidden.");
            }

            if (forbiddableComp.Forbidden == forbid.Value)
            {
                return ExecutionResult.Failure($"Item '{itemName}' is already {(forbid.Value ? "forbidden" : "unforbidden")}.");
            }

            parsedData = new ForbidItemData { Item = item, Forbid = forbid.Value };
            return ExecutionResult.Success($"Queuing action to {(forbid.Value ? "forbid" : "unforbid")} {item.LabelShort}.");
        }

        protected override UniTask ExecuteAsync(ForbidItemData? data)
        {
            if (data == null) return UniTask.CompletedTask;

            data.Item.SetForbidden(data.Forbid, true);

            string status = data.Forbid ? "forbidden" : "unforbidden";
            Log.Message($"[Neuro] Executed: Item {data.Item.LabelShort} has been marked as {status}.");
            Context.Send($"{data.Item.LabelCap} is now {status}.", silent: false);

            return UniTask.CompletedTask;
        }
    }
}
