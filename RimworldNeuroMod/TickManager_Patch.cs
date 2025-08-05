using HarmonyLib;
using Verse;

namespace NeuroPlaysRimworld
{
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TickManagerUpdate))]
    public static class TickManager_Patch
    {
        public static void Postfix()
        {
            NeuroRimModStartup.Controller?.GameUpdate();
        }
    }
}