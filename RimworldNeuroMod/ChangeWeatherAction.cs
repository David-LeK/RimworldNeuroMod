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
    public class ChangeWeatherData
    {
        public WeatherDef WeatherDef = null!;
    }

    public class ChangeWeatherAction : NeuroAction<ChangeWeatherData>
    {
        private static readonly Dictionary<string, WeatherDef> AllWeathers;

        static ChangeWeatherAction()
        {
            AllWeathers = DefDatabase<WeatherDef>.AllDefs.ToDictionary(w => w.defName, w => w);
        }

        public override string Name => "change_weather";

        protected override string Description => "Changes the current weather on the map.";

        protected override JsonSchema Schema
        {
            get
            {
                return new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "weather_name" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["weather_name"] = QJS.Enum(AllWeathers.Keys.OrderBy(k => k))
                    }
                };
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out ChangeWeatherData? parsedData)
        {
            parsedData = null;
            var weatherName = actionData.Data?["weather_name"]?.Value<string>();

            if (string.IsNullOrEmpty(weatherName))
            {
                return ExecutionResult.Failure("Missing required parameter: 'weather_name'.");
            }

            if (!AllWeathers.TryGetValue(weatherName, out var weatherDef))
            {
                return ExecutionResult.Failure($"Invalid weather_name: '{weatherName}'. Please choose from the available options.");
            }

            parsedData = new ChangeWeatherData { WeatherDef = weatherDef };
            return ExecutionResult.Success($"Queuing weather change to '{weatherDef.label}'.");
        }

        protected override UniTask ExecuteAsync(ChangeWeatherData? data)
        {
            if (data != null)
            {
                var map = Find.CurrentMap;
                if (map != null)
                {
                    map.weatherManager.TransitionTo(data.WeatherDef);
                    Log.Message($"[Neuro] Executed: Changed weather to {data.WeatherDef.label}.");
                    Context.Send($"Weather changed to {data.WeatherDef.label}.", silent: true);
                }
                else
                {
                    Log.Error("[Neuro] ChangeWeatherAction failed: No map loaded.");
                }
            }
            return UniTask.CompletedTask;
        }
    }
}
