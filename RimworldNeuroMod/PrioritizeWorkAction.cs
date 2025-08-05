using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace NeuroPlaysRimworld
{
    public class WorkPriorityData
    {
        public Pawn Pawn;
        public WorkTypeDef WorkDef;
        public int Priority;
    }

    public class PrioritizeWorkAction : NeuroAction<WorkPriorityData>
    {
        public PrioritizeWorkAction() { }

        public override string Name => "set_work_priority";

        protected override string Description => "Set a work priority for a colonist. 0 disables the work, 1 is the highest priority, and 4 is the lowest.";

        protected override JsonSchema Schema
        {
            get
            {
                var map = Find.CurrentMap;
                if (map == null) return new JsonSchema();

                var colonistNames = map.mapPawns.FreeColonists.Select(p => p.Name.ToStringShort);
                var workTypeNames = DefDatabase<WorkTypeDef>.AllDefs.Select(w => w.defName);

                return new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "colonist_name", "work_type", "priority" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["colonist_name"] = QJS.Enum(colonistNames),
                        ["work_type"] = QJS.Enum(workTypeNames),
                        ["priority"] = new JsonSchema
                        {
                            Type = JsonSchemaType.Integer,
                            Enum = new List<object> { 0, 1, 2, 3, 4 }
                        }
                    }
                };
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out WorkPriorityData? parsedData)
        {
            parsedData = null;
            string colonistName = actionData.Data?["colonist_name"]?.Value<string>();
            string workTypeName = actionData.Data?["work_type"]?.Value<string>();
            int? priority = actionData.Data?["priority"]?.Value<int?>();

            if (string.IsNullOrEmpty(colonistName) || string.IsNullOrEmpty(workTypeName) || !priority.HasValue)
            {
                return ExecutionResult.Failure("Missing required parameter: 'colonist_name', 'work_type', or 'priority'.");
            }

            var pawn = Find.CurrentMap.mapPawns.FreeColonists.FirstOrDefault(p => p.Name.ToStringShort == colonistName);
            if (pawn == null)
            {
                return ExecutionResult.Failure($"Action failed. Colonist '{colonistName}' not found.");
            }

            var workDef = DefDatabase<WorkTypeDef>.GetNamed(workTypeName, false);
            if (workDef == null)
            {
                return ExecutionResult.Failure($"Action failed. Work type '{workTypeName}' not found.");
            }

            if (pawn.WorkTypeIsDisabled(workDef))
            {
                return ExecutionResult.Failure($"Action failed. {colonistName} is incapable of {workTypeName}.");
            }

            parsedData = new WorkPriorityData { Pawn = pawn, WorkDef = workDef, Priority = priority.Value };
            return ExecutionResult.Success();
        }

        protected override UniTask ExecuteAsync(WorkPriorityData? data)
        {
            if (data != null)
            {
                var pawn = data.Pawn;
                var workDef = data.WorkDef;
                int newPriority = data.Priority;

                pawn.workSettings.SetPriority(workDef, newPriority);

                Log.Message($"[Neuro] Executed: {pawn.Name.ToStringShort}'s priority for {workDef.labelShort} is now {newPriority}.");
            }
            return UniTask.CompletedTask;
        }
    }
}
