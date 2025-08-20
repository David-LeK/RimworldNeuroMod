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
using UnityEngine;
using Verse;
using static System.Net.Mime.MediaTypeNames;

namespace NeuroPlaysRimworld
{
    public class DropPodRaidData
    {
        public Faction Faction = null!;
        public float Points;
        public int Radius;
        public bool IsHostile;
    }

    public class DropPodRaidAction : NeuroAction<DropPodRaidData>
    {
        public override string Name => "drop_pod_raid_random";

        protected override string Description => "Initiates a drop pod raid. Select a faction and its raid type (Hostile/Neutral) from the list.";

        protected override JsonSchema Schema
        {
            get
            {
                var map = Find.CurrentMap;
                if (map == null) return new JsonSchema();

                var factionChoices = new List<string>();
                var potentialFactions = Find.FactionManager.AllFactions;

                foreach (var faction in potentialFactions)
                {
                    bool isHostile = faction.HostileTo(Faction.OfPlayer);
                    string hostilityLabel = isHostile ? "Hostile" : "Neutral";
                    factionChoices.Add($"{faction.Name} ({hostilityLabel})");
                }
                factionChoices.Sort();

                var points = DebugActionsUtility.PointsOptions(true).Cast<object>().ToList();
                var radii = DebugActionsUtility.RadiusOptions().Cast<object>().ToList();

                return new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "faction_choice", "points", "radius" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["faction_choice"] = QJS.Enum(factionChoices),
                        ["points"] = new JsonSchema { Enum = points },
                        ["radius"] = new JsonSchema { Enum = radii }
                    }
                };
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out DropPodRaidData? parsedData)
        {
            parsedData = null;
            var factionChoice = actionData.Data?["faction_choice"]?.Value<string>();
            var points = actionData.Data?["points"]?.Value<float?>();
            var radius = actionData.Data?["radius"]?.Value<int?>();

            if (string.IsNullOrEmpty(factionChoice) || !points.HasValue || !radius.HasValue)
            {
                return ExecutionResult.Failure("Missing required parameters: 'faction_choice', 'points', or 'radius'.");
            }

            var lastOpenParen = factionChoice.LastIndexOf('(');
            if (lastOpenParen == -1 || !factionChoice.EndsWith(")"))
            {
                return ExecutionResult.Failure($"Invalid faction_choice format: '{factionChoice}'. Expected 'Faction Name (Hostility)'.");
            }

            var factionName = factionChoice.Substring(0, lastOpenParen).Trim();
            var hostilityString = factionChoice.Substring(lastOpenParen + 1, factionChoice.Length - lastOpenParen - 2);

            bool isHostileChoice;
            if (hostilityString.Equals("Hostile", StringComparison.OrdinalIgnoreCase))
            {
                isHostileChoice = true;
            }
            else if (hostilityString.Equals("Neutral", StringComparison.OrdinalIgnoreCase))
            {
                isHostileChoice = false;
            }
            else
            {
                return ExecutionResult.Failure($"Invalid hostility type '{hostilityString}' in faction_choice.");
            }

            var faction = Find.FactionManager.AllFactions.FirstOrDefault(f => f.Name == factionName);
            if (faction == null)
            {
                return ExecutionResult.Failure($"Could not find a faction named '{factionName}'.");
            }

            if (!DebugActionsUtility.PointsOptions(true).Contains(points.Value))
            {
                return ExecutionResult.Failure($"Invalid points value: {points.Value}.");
            }

            if (!DebugActionsUtility.RadiusOptions().Contains(radius.Value))
            {
                return ExecutionResult.Failure($"Invalid radius value: {radius.Value}.");
            }

            parsedData = new DropPodRaidData
            {
                Faction = faction,
                Points = points.Value,
                Radius = radius.Value,
                IsHostile = isHostileChoice
            };
            return ExecutionResult.Success($"Queuing {hostilityString} drop pod raid from '{faction.Name}' with {points.Value} points.");
        }

        protected override UniTask ExecuteAsync(DropPodRaidData? data)
        {
            if (data == null) return UniTask.CompletedTask;

            var map = Find.CurrentMap;
            if (map == null)
            {
                Log.Error("[Neuro] DropPodRaidAction failed: No map loaded.");
                return UniTask.CompletedTask;
            }

            if (!RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(
              c => c.Standable(map) && !c.Roofed(map) && map.reachability.CanReachColony(c),
              map,
              out var spawnCenter))
            {
                Log.Warning("[Neuro] Could not find a suitable random location for the drop pod raid. Using map center as fallback.");
                spawnCenter = map.Center;
            }

            bool isHostile = data.IsHostile;

            var raidStrategy = isHostile ? RaidStrategyDefOf.ImmediateAttack : RaidStrategyDefOf.ImmediateAttackFriendly;
            var raidArrivalMode = isHostile ? PawnsArrivalModeDefOf.EdgeDrop : PawnsArrivalModeDefOf.CenterDrop;

            var parms = new IncidentParms
            {
                target = map,
                faction = data.Faction,
                points = data.Points,
                raidStrategy = raidStrategy,
                raidArrivalMode = raidArrivalMode,
                spawnCenter = spawnCenter,
                dropInRadius = data.Radius,
                forced = true,
                silent = true
            };

            var incidentDef = isHostile ? IncidentDefOf.RaidEnemy : IncidentDefOf.RaidFriendly;
            incidentDef.Worker.TryExecute(parms);

            string raidType = isHostile ? "hostile" : "neutral";
            Log.Message($"[Neuro] Executed {raidType} drop pod raid from {data.Faction.Name} with {data.Points} points at ({spawnCenter.x}, {spawnCenter.z}) with radius {data.Radius}.");
            Context.Send($"✅ Dropped a {raidType} raid from {data.Faction.Name} at a random location.", silent: true);

            return UniTask.CompletedTask;
        }
    }
}