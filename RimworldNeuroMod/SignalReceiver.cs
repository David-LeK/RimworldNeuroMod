#nullable enable

using NeuroSdk.Messages.Outgoing;
using RimWorld;
using Verse;

namespace NeuroPlaysRimworld
{
    public class SignalReceiver : GameComponent, ISignalReceiver
    {
        public SignalReceiver(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Find.SignalManager.RegisterReceiver(this);
        }

        public void Notify_SignalReceived(Signal signal)
        {
            var controller = NeuroRimModStartup.Controller;
            if (controller == null) return;

            switch (signal.tag)
            {
                case ResearchManager.ResearchCompletedSignal:
                    if (signal.args.TryGetArg("project", out NamedArgument projectArg) && projectArg.arg is ResearchProjectDef project)
                    {
                        controller.HandleGameEvent($"[RESEARCH] Completed: {project.LabelCap}. New technologies may be available.", silent: false);
                        controller.RefreshAction<SetResearchProjectAction>();
                    }
                    break;

                case "Neuro.AlertFired":
                    if (signal.args.TryGetArg("alertText", out NamedArgument alertTextArg) && alertTextArg.arg is string alertText)
                    {
                        controller.HandleGameEvent($"[ALERT] {alertText}", silent: false);
                        controller.RefreshAction<FightAction>();
                        controller.RefreshAction<SetColonistDraftStatusAction>();
                    }
                    break;

                case "Neuro.PawnDied":
                    if (signal.args.TryGetArg("pawn", out NamedArgument pawnArg) && pawnArg.arg is Pawn pawn)
                    {
                        string factionInfo = pawn.Faction == Faction.OfPlayer ? "colonist" : "enemy";
                        controller.HandleGameEvent($"[EVENT] The {factionInfo} {pawn.LabelShortCap} has died.", silent: false);
                        controller.RefreshAction<FightAction>();
                        controller.RefreshAction<ArmIndividuallyAction>();
                        controller.RefreshAction<PrioritizeWorkAction>();
                    }
                    break;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }
    }
}
