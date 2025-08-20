using Verse;

namespace NeuroPlaysRimworld
{
    public enum NeuroMode
    {
        Storyteller,
        Player
    }

    public class NeuroRimModSettings : ModSettings
    {
        public string websocketUrl = "";
        public NeuroMode selectedMode = NeuroMode.Storyteller;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref websocketUrl, "websocketUrl", "");
            Scribe_Values.Look(ref selectedMode, "selectedMode", NeuroMode.Storyteller);
        }
    }
}