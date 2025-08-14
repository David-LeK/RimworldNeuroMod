#nullable enable

using NeuroPlaysRimworld;
using RimWorld;
using Verse;

namespace NeuroPlaysRimworld
{
    public class SignalReceiver : GameComponent, ISignalReceiver
    {
        public SignalReceiver(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Find.SignalManager.RegisterReceiver(this);
        }

        public void Notify_SignalReceived(Signal signal)
        {
            switch (signal.tag)
            {
                case ResearchManager.ResearchCompletedSignal:
                    Log.Message($"[Neuro] Signal received: '{signal.tag}'. Refreshing relevant action schema.");
                    NeuroRimModStartup.Controller?.RefreshAction<SetResearchProjectAction>();
                    break;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }
    }
}