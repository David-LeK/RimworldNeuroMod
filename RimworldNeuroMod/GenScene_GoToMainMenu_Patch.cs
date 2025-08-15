using HarmonyLib;
using Verse;

namespace NeuroPlaysRimworld
{
    [HarmonyPatch(typeof(GenScene), nameof(GenScene.GoToMainMenu))]
    public static class GenScene_GoToMainMenu_Patch
    {
        public static void Prefix()
        {
            if (NeuroRimModStartup.Controller != null)
            {
                Log.Message("[Neuro] Player is returning to the main menu. Unregistering game actions.");
                NeuroRimModStartup.Controller.UnregisterGameActions();
            }
        }
    }
}