#nullable enable

using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace NeuroPlaysRimworld
{
    [HarmonyPatch(typeof(AlertsReadout), "CheckAddOrRemoveAlert")]
    public static class Patch_Alerts
    {
        public static void Prefix(Alert alert, List<Alert> ___activeAlerts, out bool __state)
        {
            __state = ___activeAlerts.Contains(alert);
        }

        public static void Postfix(Alert alert, List<Alert> ___activeAlerts, bool __state)
        {
            bool isNewAlert = !__state && ___activeAlerts.Contains(alert);

            if (!isNewAlert || alert.Priority < AlertPriority.High)
            {
                return;
            }

            string alertText = alert.GetLabel();
            if (!string.IsNullOrEmpty(alertText))
            {
                Find.SignalManager.SendSignal(new Signal("Neuro.AlertFired", new NamedArgument(alertText, "alertText")));
            }
        }
    }
}