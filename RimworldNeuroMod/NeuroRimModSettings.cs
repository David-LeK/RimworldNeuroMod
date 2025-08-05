using Verse;

namespace NeuroPlaysRimworld
{
    public class NeuroRimModSettings : ModSettings
    {
        public string websocketUrl = "";

        public override void ExposeData()
        {
            Scribe_Values.Look(ref websocketUrl, "websocketUrl", "");
            base.ExposeData();
        }
    }
}