#nullable enable

using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace NeuroPlaysRimworld
{
    public class SetResearchProjectData
    {
        public ResearchProjectDef ProjectDef = null!;
    }

    public class SetResearchProjectAction : NeuroAction<SetResearchProjectData>
    {
        public override string Name => "set_research_project";

        protected override string Description => "Selects the colony's active research project from the list of available technologies.";

        protected override JsonSchema Schema
        {
            get
            {
                var availableProjects = DefDatabase<ResearchProjectDef>.AllDefs
                    .Where(p => p.CanStartNow)
                    .Select(p => p.defName)
                    .OrderBy(name => name);

                return new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "project_def_name" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["project_def_name"] = QJS.Enum(availableProjects)
                    }
                };
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out SetResearchProjectData? parsedData)
        {
            parsedData = null;
            var projectName = actionData.Data?["project_def_name"]?.Value<string>();

            if (string.IsNullOrEmpty(projectName))
            {
                return ExecutionResult.Failure("Missing required parameter: 'project_def_name'.");
            }

            var projectDef = DefDatabase<ResearchProjectDef>.GetNamed(projectName, errorOnFail: false);
            if (projectDef == null)
            {
                return ExecutionResult.Failure($"Invalid research project name: '{projectName}'.");
            }

            if (!projectDef.CanStartNow)
            {
                return ExecutionResult.Failure($"Cannot start research project '{projectDef.label}': Prerequisites not met.");
            }

            parsedData = new SetResearchProjectData { ProjectDef = projectDef };
            return ExecutionResult.Success($"Queuing research project change to '{projectDef.label}'.");
        }

        protected override UniTask ExecuteAsync(SetResearchProjectData? data)
        {
            if (data != null)
            {
                if (Find.ResearchManager.IsCurrentProject(data.ProjectDef))
                {
                    Log.Message($"[Neuro] Research project is already set to {data.ProjectDef.label}. No action taken.");
                    Context.Send($"⚠️ Research project is already {data.ProjectDef.label}.", silent: true);
                }
                else
                {
                    Find.ResearchManager.SetCurrentProject(data.ProjectDef);
                    Log.Message($"[Neuro] Executed: Changed research project to {data.ProjectDef.label}.");
                    Context.Send($"✅ Research project changed to {data.ProjectDef.label}.", silent: true);
                }
            }
            return UniTask.CompletedTask;
        }
    }
}