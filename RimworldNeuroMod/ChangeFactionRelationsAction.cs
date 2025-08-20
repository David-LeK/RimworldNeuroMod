#nullable enable

using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace NeuroPlaysRimworld
{
    public class ChangeFactionRelationsData
    {
        public Faction Faction = null!;
        public FactionRelationKind NewRelation;
    }

    public class ChangeFactionRelationsAction : NeuroAction<ChangeFactionRelationsData>
    {
        public override string Name => "change_faction_relations";

        protected override string Description => "A powerful world-level action to instantly alter the relationship with a specific non-player faction.";

        protected override JsonSchema Schema
        {
            get
            {
                var factionNames = Find.FactionManager.AllFactions
                    .Where(f => !f.IsPlayer && !f.def.hidden)
                    .Select(f => f.Name)
                    .OrderBy(name => name);

                var relationStatuses = Enum.GetNames(typeof(FactionRelationKind));

                return new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "faction_name", "new_relation_status" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["faction_name"] = QJS.Enum(factionNames),
                        ["new_relation_status"] = QJS.Enum(relationStatuses)
                    }
                };
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out ChangeFactionRelationsData? parsedData)
        {
            parsedData = null;
            var factionName = actionData.Data?["faction_name"]?.Value<string>();
            var newRelationStatusStr = actionData.Data?["new_relation_status"]?.Value<string>();

            if (string.IsNullOrEmpty(factionName) || string.IsNullOrEmpty(newRelationStatusStr))
            {
                return ExecutionResult.Failure("Missing required parameters: 'faction_name' or 'new_relation_status'.");
            }

            var faction = Find.FactionManager.AllFactions.FirstOrDefault(f => f.Name == factionName);
            if (faction == null)
            {
                return ExecutionResult.Failure($"Faction '{factionName}' not found.");
            }
            if (faction.IsPlayer)
            {
                return ExecutionResult.Failure("Cannot change relations with your own faction.");
            }

            if (!Enum.TryParse<FactionRelationKind>(newRelationStatusStr, true, out var newRelation))
            {
                return ExecutionResult.Failure($"Invalid relation status: '{newRelationStatusStr}'.");
            }

            if (faction.def.permanentEnemy && newRelation != FactionRelationKind.Hostile)
            {
                return ExecutionResult.Failure($"Cannot change the relationship with '{factionName}'; they are permanently hostile.");
            }

            if (faction.RelationKindWith(Faction.OfPlayer) == newRelation)
            {
                return ExecutionResult.Failure($"The relationship with '{factionName}' is already {newRelationStatusStr}.");
            }

            parsedData = new ChangeFactionRelationsData { Faction = faction, NewRelation = newRelation };
            return ExecutionResult.Success($"Queuing relation change for '{factionName}' to {newRelationStatusStr}.");
        }

        protected override UniTask ExecuteAsync(ChangeFactionRelationsData? data)
        {
            if (data == null) return UniTask.CompletedTask;

            var playerFaction = Faction.OfPlayer;
            var targetFaction = data.Faction;

            int targetGoodwill = 0;
            switch (data.NewRelation)
            {
                case FactionRelationKind.Hostile:
                    targetGoodwill = -100;
                    break;
                case FactionRelationKind.Neutral:
                    targetGoodwill = 0;
                    break;
                case FactionRelationKind.Ally:
                    targetGoodwill = 100;
                    break;
            }

            int currentGoodwill = targetFaction.GoodwillWith(playerFaction);
            int goodwillChange = targetGoodwill - currentGoodwill;

            playerFaction.TryAffectGoodwillWith(targetFaction, goodwillChange, canSendMessage: false, canSendHostilityLetter: false);

            string message = $"🤝 Your relationship with {targetFaction.Name} has changed to {data.NewRelation}.";
            Log.Message($"[Neuro] Executed: Changed relationship with {targetFaction.Name} to {data.NewRelation} by adjusting goodwill by {goodwillChange}.");
            Context.Send(message, silent: false);
            NeuroRimModStartup.Controller?.RefreshAction<DropPodRaidAction>();
            return UniTask.CompletedTask;
        }
    }
}