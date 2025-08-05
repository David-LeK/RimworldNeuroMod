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

namespace NeuroPlaysRimworld
{
    public class SetColonistDraftStatusData
    {
        public Pawn Pawn = null!;
        public bool IsDrafted;
    }

    public class SetColonistDraftStatusAction : NeuroAction<SetColonistDraftStatusData>
    {
        public override string Name => "set_colonist_draft_status";

        protected override string Description => "Toggles a colonist's drafted mode. Colonists incapable of violence cannot be drafted.";

        protected override JsonSchema Schema
        {
            get
            {
                var map = Find.CurrentMap;
                if (map == null) return new JsonSchema();

                var allColonists = map.mapPawns.FreeColonists
                    .Select(p => p.Name.ToStringShort);

                return new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "colonist_name", "is_drafted" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["colonist_name"] = QJS.Enum(allColonists),
                        ["is_drafted"] = new JsonSchema { Type = JsonSchemaType.Boolean }
                    }
                };
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out SetColonistDraftStatusData? parsedData)
        {
            parsedData = null;
            var colonistName = actionData.Data?["colonist_name"]?.Value<string>();
            var isDrafted = actionData.Data?["is_drafted"]?.Value<bool?>();

            if (string.IsNullOrEmpty(colonistName) || !isDrafted.HasValue)
            {
                return ExecutionResult.Failure("Missing required parameters: 'colonist_name' or 'is_drafted'.");
            }

            var pawn = Find.CurrentMap?.mapPawns.FreeColonists.FirstOrDefault(p => p.Name.ToStringShort == colonistName);
            if (pawn == null)
            {
                return ExecutionResult.Failure($"Colonist '{colonistName}' not found.");
            }

            if (pawn.drafter == null)
            {
                return ExecutionResult.Failure($"{colonistName} cannot be drafted (missing drafter component).");
            }

            if (pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return ExecutionResult.Failure($"Action failed. {colonistName} is incapable of violence and cannot be drafted.");
            }

            if (pawn.Downed)
            {
                return ExecutionResult.Failure($"{colonistName} is downed and cannot be drafted.");
            }

            if (isDrafted.Value && pawn.InMentalState)
            {
                return ExecutionResult.Failure($"{colonistName} is having a mental break and cannot be drafted.");
            }

            parsedData = new SetColonistDraftStatusData { Pawn = pawn, IsDrafted = isDrafted.Value };
            return ExecutionResult.Success($"Queuing draft status change for {colonistName}.");
        }

        protected override UniTask ExecuteAsync(SetColonistDraftStatusData? data)
        {
            if (data == null)
            {
                return UniTask.CompletedTask;
            }

            var pawn = data.Pawn;
            pawn.drafter.Drafted = data.IsDrafted;

            string status = data.IsDrafted ? "drafted" : "undrafted";
            Log.Message($"[Neuro] Executed: {pawn.Name.ToStringShort} has been {status}.");
            Context.Send($"{pawn.Name.ToStringShort} is now {status}.", silent: false);

            return UniTask.CompletedTask;
        }
    }
}
