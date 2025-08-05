#nullable enable

using HarmonyLib;
using NeuroSdk;
using System;
using UnityEngine;
using Verse;

namespace NeuroPlaysRimworld
{
    public class NeuroRimMod : Mod
    {
        public static NeuroRimModSettings settings;

        public NeuroRimMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<NeuroRimModSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.Label("Neuro's Power Level:");
            listingStandard.Gap(6f);

            Rect storytellerRect = listingStandard.GetRect(24f);
            if (Widgets.RadioButtonLabeled(storytellerRect, "Storyteller (God Mode)", settings.selectedMode == NeuroMode.Storyteller))
            {
                settings.selectedMode = NeuroMode.Storyteller;
            }
            TooltipHandler.TipRegion(storytellerRect, "Allows Neuro to use powerful, cheat-like actions like spawning items, raids, and changing the weather.");

            Rect playerRect = listingStandard.GetRect(24f);
            if (Widgets.RadioButtonLabeled(playerRect, "Player (Normal Mode)", settings.selectedMode == NeuroMode.Player))
            {
                settings.selectedMode = NeuroMode.Player;
            }
            TooltipHandler.TipRegion(playerRect, "Restricts Neuro to actions a normal player can perform, like managing work, drafting colonists, and designating targets.");

            listingStandard.Gap(24f);
            listingStandard.Label("Advanced Connection Settings");
            listingStandard.GapLine();
            listingStandard.Gap(12f);

            listingStandard.Label("Custom WebSocket URL:");

            Widgets.Label(listingStandard.GetRect(22f), "This will be used if the NEURO_SDK_WS_URL environment variable is not set.");

            settings.websocketUrl = listingStandard.TextEntry(settings.websocketUrl);

            listingStandard.Gap(12f);

            string? envUrl = Environment.GetEnvironmentVariable("NEURO_SDK_WS_URL");
            if (!string.IsNullOrWhiteSpace(envUrl))
            {
                GUI.color = Color.yellow;
                listingStandard.Label($"Warning: The environment variable is set and will override this setting. The game is currently using: {envUrl}");
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.gray;
                listingStandard.Label("Note: A restart is required for changes to take effect.");
                GUI.color = Color.white;
            }

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Neuro Plays RimWorld";
        }
    }

    [StaticConstructorOnStartup]
    public static class NeuroRimModStartup
    {
        public static NeuroController? Controller { get; private set; }

        static NeuroRimModStartup()
        {
            var harmony = new Harmony("com.davidlek.neuroplaysrimworld.patch");
            harmony.PatchAll();
            Log.Message("[Neuro] Harmony patches applied.");

            string finalWebsocketUrl;

            string? envUrl = Environment.GetEnvironmentVariable("NEURO_SDK_WS_URL");

            if (!string.IsNullOrWhiteSpace(envUrl))
            {
                finalWebsocketUrl = envUrl;
                Log.Message($"[Neuro] Using WebSocket URL from environment variable: {finalWebsocketUrl}");
            }
            else
            {
                var settings = LoadedModManager.GetMod<NeuroRimMod>().GetSettings<NeuroRimModSettings>();
                string customUrl = settings.websocketUrl;

                if (!string.IsNullOrWhiteSpace(customUrl))
                {
                    finalWebsocketUrl = customUrl;
                    Log.Message($"[Neuro] Using custom WebSocket URL from mod settings: {finalWebsocketUrl}");
                }
                else
                {
                    finalWebsocketUrl = "ws://localhost:8000";
                    Log.Message($"[Neuro] No environment variable or custom URL set. Using default WebSocket URL: {finalWebsocketUrl}");
                }
            }

            Environment.SetEnvironmentVariable("NEURO_SDK_WS_URL", finalWebsocketUrl, EnvironmentVariableTarget.Process);

            Log.Message("[Neuro] Initializing SDK...");
            NeuroSdkSetup.Initialize("RimWorld");

            var neuroSdkObject = GameObject.Find("NeuroSdk");
            if (neuroSdkObject != null)
            {
                Controller = neuroSdkObject.AddComponent<NeuroController>();
                Log.Message("[Neuro] NeuroController component added and ready.");
            }
            else
            {
                Log.Error("[Neuro] Could not find NeuroSdk GameObject to attach controller!");
            }
        }
    }
}