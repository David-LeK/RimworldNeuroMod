// File: RimworldNeuroMod/ForceMentalBreakAction.cs
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
    public class ForceMentalBreakData
    {
        public Pawn Pawn = null!;
        public Def? BreakDef; // Can be MentalBreakDef or InspirationDef
    }

    public class ForceMentalBreakAction : NeuroAction<ForceMentalBreakData>
    {
        private static readonly Dictionary<string, MentalBreakDef> AllMentalBreaks;
        private static readonly Dictionary<string, InspirationDef> AllInspirations;
        private static readonly List<string> AllBreakAndInspirationNames;

        static ForceMentalBreakAction()
        {
            AllMentalBreaks = DefDatabase<MentalBreakDef>.AllDefs.ToDictionary(b => b.defName, b => b);
            AllInspirations = DefDatabase<InspirationDef>.AllDefs.ToDictionary(i => i.defName, i => i);

            AllBreakAndInspirationNames = AllMentalBreaks.Keys
                .Concat(AllInspirations.Keys)
                .OrderBy(name => name)
                .ToList();
        }

        public override string Name => "force_mental_break";

        protected override string Description => "A classic storyteller action to trigger a specific mental break (or inspiration) on a chosen colonist.";

        protected override JsonSchema Schema
        {
            get
            {
                var map = Find.CurrentMap;
                if (map == null) return new JsonSchema();

                var colonistNames = map.mapPawns.FreeColonists.Select(p => p.Name.ToStringShort);

                return new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "colonist_name", "mental_break" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["colonist_name"] = QJS.Enum(colonistNames),
                        ["mental_break"] = QJS.Enum(AllBreakAndInspirationNames)
                    }
                };
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out ForceMentalBreakData? parsedData)
        {
            parsedData = null;
            var colonistName = actionData.Data?["colonist_name"]?.Value<string>();
            var breakName = actionData.Data?["mental_break"]?.Value<string>();

            if (string.IsNullOrEmpty(colonistName) || string.IsNullOrEmpty(breakName))
            {
                return ExecutionResult.Failure("Missing required parameters: 'colonist_name' or 'mental_break'.");
            }

            var pawn = Find.CurrentMap?.mapPawns.FreeColonists.FirstOrDefault(p => p.Name.ToStringShort == colonistName);
            if (pawn == null)
            {
                return ExecutionResult.Failure($"Colonist '{colonistName}' not found.");
            }

            // Check for mental breaks
            if (AllMentalBreaks.TryGetValue(breakName, out var mentalBreakDef))
            {
                if (pawn.InMentalState) return ExecutionResult.Failure($"{colonistName} is already in a mental state.");
                if (pawn.Downed) return ExecutionResult.Failure($"{colonistName} is downed and cannot have a mental break.");
                if (pawn.mindState.mentalBreaker.Blocked) return ExecutionResult.Failure($"{colonistName}'s mental breaks are currently blocked (e.g., by a psychic soother).");
                if (!pawn.Awake()) return ExecutionResult.Failure($"{colonistName} is asleep and cannot have a mental break.");

                if (!mentalBreakDef.Worker.BreakCanOccur(pawn))
                {
                    return ExecutionResult.Failure($"Action failed. {colonistName} cannot have the mental break '{breakName}' under current conditions (e.g., traits, health).");
                }
                parsedData = new ForceMentalBreakData { Pawn = pawn, BreakDef = mentalBreakDef };
                return ExecutionResult.Success($"Queuing mental break '{breakName}' for {colonistName}.");
            }

            // Check for inspirations
            if (AllInspirations.TryGetValue(breakName, out var inspirationDef))
            {
                if (pawn.Inspired) return ExecutionResult.Failure($"{colonistName} is already inspired.");
                if (pawn.Downed) return ExecutionResult.Failure($"{colonistName} is downed and cannot be inspired.");

                if (!inspirationDef.Worker.InspirationCanOccur(pawn))
                {
                    return ExecutionResult.Failure($"Action failed. {colonistName} cannot get the inspiration '{breakName}' under current conditions (e.g., traits, mood).");
                }
                parsedData = new ForceMentalBreakData { Pawn = pawn, BreakDef = inspirationDef };
                return ExecutionResult.Success($"Queuing inspiration '{breakName}' for {colonistName}.");
            }

            return ExecutionResult.Failure($"Invalid mental break or inspiration name: '{breakName}'.");
        }

        protected override UniTask ExecuteAsync(ForceMentalBreakData? data)
        {
            if (data?.Pawn == null || data.BreakDef == null)
            {
                return UniTask.CompletedTask;
            }

            var pawn = data.Pawn;

            if (data.BreakDef is MentalBreakDef mentalBreakDef)
            {
                if (pawn.mindState.mentalBreaker.TryDoMentalBreak("Neuro's will.", mentalBreakDef))
                {
                    Log.Message($"[Neuro] Executed: Forced mental break '{mentalBreakDef.defName}' on {pawn.Name.ToStringShort}.");
                    Context.Send($"😈 {pawn.Name.ToStringShort} is having a mental break: {mentalBreakDef.label.CapitalizeFirst()}", silent: false);
                }
                else
                {
                    Log.Warning($"[Neuro] Failed to force mental break '{mentalBreakDef.defName}' on {pawn.Name.ToStringShort}. TryDoMentalBreak returned false, likely due to a last-minute state change.");
                    Context.Send($"⚠️ Could not force the mental break on {pawn.Name.ToStringShort}.", silent: true);
                }
            }
            else if (data.BreakDef is InspirationDef inspirationDef)
            {
                if (pawn.mindState.inspirationHandler.TryStartInspiration(inspirationDef, "Neuro's gift."))
                {
                    Log.Message($"[Neuro] Executed: Forced inspiration '{inspirationDef.defName}' on {pawn.Name.ToStringShort}.");
                    Context.Send($"✨ {pawn.Name.ToStringShort} has been inspired: {inspirationDef.label.CapitalizeFirst()}", silent: false);
                }
                else
                {
                    Log.Warning($"[Neuro] Failed to force inspiration '{inspirationDef.defName}' on {pawn.Name.ToStringShort}. TryStartInspiration returned false.");
                    Context.Send($"⚠️ Could not grant the inspiration to {pawn.Name.ToStringShort}.", silent: true);
                }
            }

            return UniTask.CompletedTask;
        }
    }
}