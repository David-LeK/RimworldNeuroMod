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
using System.Text.RegularExpressions;
using Verse;
using Verse.AI;

namespace NeuroPlaysRimworld
{
    public enum AnimalActionType { Hunt, Tame, Slaughter }

    public class ManageAnimalData
    {
        public Pawn AnimalPawn = null!;
        public AnimalActionType Action;
    }

    public class ManageAnimalAction : NeuroAction<ManageAnimalData>
    {
        public override string Name => "manage_animal";
        protected override string Description => "Manage animals on the map. Designate wild animals to be hunted or tamed, or colony animals to be slaughtered.";

        private static string GetTargetDisplayName(Pawn pawn)
        {
            // Provides a unique name for the dropdown, e.g., "Muffalo (ID: Animal_Muffalo_123)"
            return $"{pawn.LabelShortCap} (ID: {pawn.ThingID})";
        }

        protected override JsonSchema Schema
        {
            get
            {
                var map = Find.CurrentMap;
                if (map == null) return new JsonSchema();

                var allAnimals = map.mapPawns.AllPawnsSpawned
                    .Where(p => p.RaceProps.Animal && !p.Downed && p.Position.IsValid && map.reachability.CanReachColony(p.Position))
                    .OrderBy(p => p.Label)
                    .Select(GetTargetDisplayName);

                return new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "animal_pawn", "action_type" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["animal_pawn"] = QJS.Enum(allAnimals),
                        ["action_type"] = QJS.Enum(Enum.GetNames(typeof(AnimalActionType)))
                    }
                };
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out ManageAnimalData? parsedData)
        {
            parsedData = null;
            var map = Find.CurrentMap;
            if (map == null) return ExecutionResult.Failure("No map loaded.");

            var animalDisplayName = actionData.Data?["animal_pawn"]?.Value<string>();
            var actionTypeString = actionData.Data?["action_type"]?.Value<string>();

            if (string.IsNullOrEmpty(animalDisplayName) || string.IsNullOrEmpty(actionTypeString))
            {
                return ExecutionResult.Failure("Missing required parameters: 'animal_pawn' or 'action_type'.");
            }

            // Extract the ThingID from the display name
            var match = Regex.Match(animalDisplayName, @"\(ID: ([^)]+)\)$");
            if (!match.Success)
            {
                return ExecutionResult.Failure($"Invalid animal_pawn format: '{animalDisplayName}'.");
            }
            string animalId = match.Groups[1].Value;

            // Find the animal using its unique ID
            var animal = map.listerThings.AllThings.FirstOrDefault(t => t.ThingID == animalId) as Pawn;

            if (animal == null || animal.Destroyed || animal.Dead || animal.Downed)
            {
                return ExecutionResult.Failure($"Animal '{animalDisplayName}' is no longer available.");
            }
            if (!animal.RaceProps.Animal)
            {
                return ExecutionResult.Failure($"Target '{animalDisplayName}' is not an animal.");
            }

            if (!Enum.TryParse<AnimalActionType>(actionTypeString, true, out var actionType))
            {
                return ExecutionResult.Failure($"Invalid action_type: '{actionTypeString}'.");
            }

            // Validate the action against the animal's state
            switch (actionType)
            {
                case AnimalActionType.Hunt:
                    if (animal.Faction == Faction.OfPlayer)
                        return ExecutionResult.Failure($"Cannot hunt a tamed animal: {animal.LabelShort}.");
                    if (map.designationManager.DesignationOn(animal, DesignationDefOf.Hunt) != null)
                        return ExecutionResult.Failure($"{animal.LabelShort} is already designated for hunting.");
                    break;

                case AnimalActionType.Tame:
                    if (animal.Faction == Faction.OfPlayer)
                        return ExecutionResult.Failure($"{animal.LabelShort} is already tamed.");
                    if (animal.RaceProps.trainability == TrainabilityDefOf.None)
                        return ExecutionResult.Failure($"{animal.LabelShort} cannot be tamed.");
                    if (map.designationManager.DesignationOn(animal, DesignationDefOf.Tame) != null)
                        return ExecutionResult.Failure($"{animal.LabelShort} is already designated for taming.");
                    break;

                case AnimalActionType.Slaughter:
                    if (animal.Faction != Faction.OfPlayer)
                        return ExecutionResult.Failure($"Cannot slaughter a wild animal: {animal.LabelShort}.");
                    if (map.designationManager.DesignationOn(animal, DesignationDefOf.Slaughter) != null)
                        return ExecutionResult.Failure($"{animal.LabelShort} is already designated for slaughter.");
                    break;
            }

            parsedData = new ManageAnimalData { AnimalPawn = animal, Action = actionType };
            return ExecutionResult.Success($"Queuing {actionType} action for {animal.LabelShort}.");
        }

        protected override UniTask ExecuteAsync(ManageAnimalData? data)
        {
            if (data == null) return UniTask.CompletedTask;

            var map = Find.CurrentMap;
            var animal = data.AnimalPawn;
            if (map == null || animal == null) return UniTask.CompletedTask;

            switch (data.Action)
            {
                case AnimalActionType.Hunt:
                    map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Hunt));
                    Log.Message($"[Neuro] Designated {animal.LabelShort} for hunting.");
                    Context.Send($"Designating {animal.LabelShort} to be hunted for food.", silent: false);
                    break;

                case AnimalActionType.Tame:
                    map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Tame));
                    Log.Message($"[Neuro] Designated {animal.LabelShort} for taming.");
                    Context.Send($"Designating {animal.LabelShort} to be tamed.", silent: false);
                    break;

                case AnimalActionType.Slaughter:
                    map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Slaughter));
                    Log.Message($"[Neuro] Designated {animal.LabelShort} for slaughter.");
                    Context.Send($"Designating {animal.LabelShort} for slaughter.", silent: false);
                    break;
            }

            return UniTask.CompletedTask;
        }
    }
}
