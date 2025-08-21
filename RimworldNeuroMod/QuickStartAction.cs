#nullable enable

using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using RimWorld;
using System;
using Verse;

namespace NeuroPlaysRimworld
{
    public class QuickStartData { }

    public class QuickStartAction : NeuroAction<QuickStartData>
    {
        public override string Name => "quick_start";

        protected override string Description => "Starts a new quick game with default settings, using the game's built-in quick-test function.";

        protected override JsonSchema Schema => new JsonSchema { Type = JsonSchemaType.Object };

        protected override ExecutionResult Validate(ActionJData actionData, out QuickStartData? parsedData)
        {
            parsedData = null;
            if (Current.Game != null)
            {
                return ExecutionResult.Failure("Cannot start a quick game while another game is already loaded.");
            }

            parsedData = new QuickStartData();
            return ExecutionResult.Success("Starting a new quick game.");
        }

        protected override UniTask ExecuteAsync(QuickStartData? data)
        {
            if (data == null) return UniTask.CompletedTask;

            LongEventHandler.QueueLongEvent(() =>
            {
                Root_Play.SetupForQuickTestPlay();
                PageUtility.InitGameStart();

            }, "GeneratingMap", doAsynchronously: true, exceptionHandler: GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);

            NeuroRimModStartup.Controller?.OnQuickStartExecuted();

            Log.Message("[Neuro] Executed: Starting a quick game.");
            return UniTask.CompletedTask;
        }
    }
}