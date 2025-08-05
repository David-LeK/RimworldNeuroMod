#nullable enable

using HarmonyLib;
using RimWorld;
using Verse;

namespace NeuroPlaysRimworld
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill
    {
        public static void Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            Find.SignalManager.SendSignal(new Signal("Neuro.PawnDied", new NamedArgument(__instance, "pawn")));
        }
    }
}