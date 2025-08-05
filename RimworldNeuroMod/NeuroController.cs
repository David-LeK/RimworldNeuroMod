#nullable enable

using NeuroSdk.Actions;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace NeuroPlaysRimworld
{
    public class NeuroController : MonoBehaviour
    {
        private volatile bool isSdkReady = false;
        private bool _gameActionsRegistered = false;
        private bool _menuActionsRegistered = false;
        private readonly List<INeuroAction> _registeredGameActions = new List<INeuroAction>();
        private readonly List<INeuroAction> _registeredMenuActions = new List<INeuroAction>();

        private float fullSummaryTimer = 0f;
        private const float FULL_SUMMARY_INTERVAL = 300f; // 5 minutes

        public void Update()
        {
            if (!isSdkReady)
            {
                if (WebsocketConnection.Instance != null)
                {
                    Log.Message("[Neuro] Neuro SDK Connection Instance is ready. Controller is now active.");
                    isSdkReady = true;
                }
                else
                {
                    return;
                }
            }

            bool isMapReady = Current.Game != null && Current.Game.CurrentMap != null && Find.TickManager.TicksGame > 100;

            if (isMapReady)
            {
                if (_menuActionsRegistered) UnregisterMenuActions();
                if (!_gameActionsRegistered) RegisterGameActions();

                if (_gameActionsRegistered && !Find.TickManager.Paused)
                {
                    fullSummaryTimer += Time.deltaTime;
                    if (fullSummaryTimer >= FULL_SUMMARY_INTERVAL)
                    {
                        fullSummaryTimer = 0f;
                        string gameStateSummary = GetFullGameStateAsText();
                        Context.Send(gameStateSummary, silent: true);
                        Log.Message("[Neuro] Sent periodic full context summary.");
                        RefreshAllGameActions();
                    }
                }
            }
            else
            {
                if (_gameActionsRegistered) UnregisterGameActions();
                if (!_menuActionsRegistered) RegisterMenuActions();
            }
        }

        public void HandleGameEvent(string message, bool silent = true)
        {
            if (!isSdkReady) return;
            Context.Send(message, silent: silent);
            Log.Message($"[Neuro] Sent event context: {message}");
        }

        private string GetFullGameStateAsText()
        {
            var map = Find.CurrentMap;
            if (map == null) return "Status: No map loaded.";

            var sb = new StringBuilder();

            sb.AppendLine($"## Colony Status: {GenDate.DateFullStringAt(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile))}");
            sb.AppendLine($"Wealth: ${map.wealthWatcher.WealthTotal:F0}");

            sb.AppendLine("### Colonists");
            var colonists = map.mapPawns.FreeColonists.OrderBy(p => p.Name.ToStringShort).ToList();
            if (colonists.Any())
            {
                foreach (var p in colonists)
                {
                    string mood = p.needs?.mood != null ? $"M:{(p.needs.mood.CurLevelPercentage * 100):F0}%" : "M:N/A";
                    string job = p.CurJob != null ? p.GetJobReport() : "Idle";
                    sb.AppendLine($"- {p.Name.ToStringShort} (H:{(p.health.summaryHealth.SummaryHealthPercent * 100):F0}%, {mood}) | Task: {job}");
                }
            }
            else sb.AppendLine("None");

            var threats = map.attackTargetsCache.TargetsHostileToColony
                .Select(t => t.Thing)
                .Where(t => t is Pawn p && !p.Downed)
                .GroupBy(p => p.def.label)
                .Select(g => $"{g.Count()}x {g.Key}");

            sb.AppendLine(threats.Any() ? $"### Threats\n- " + string.Join("\n- ", threats) : "### Threats\nNone");

            var currentProj = Find.ResearchManager.GetProject();
            sb.AppendLine(currentProj != null ? $"### Research: {currentProj.label} ({(Find.ResearchManager.GetProgress(currentProj) / currentProj.baseCost):P0})" : "### Research: None");

            return sb.ToString();
        }

        public void RefreshAllGameActions()
        {
            if (!_gameActionsRegistered) return;
            Log.Message("[Neuro] Refreshing all game actions.");
            UnregisterGameActions();
            RegisterGameActions();
        }

        private void RegisterMenuActions()
        {
            if (_menuActionsRegistered) return;
            _registeredMenuActions.Clear();
            Log.Message("[Neuro] Registering main menu actions.");
            _registeredMenuActions.Add(new QuickStartAction());
            NeuroActionHandler.RegisterActions(_registeredMenuActions);
            _menuActionsRegistered = true;
        }

        private void UnregisterMenuActions()
        {
            if (!_menuActionsRegistered) return;
            Log.Message("[Neuro] Unregistering main menu actions.");
            NeuroActionHandler.UnregisterActions(_registeredMenuActions);
            _registeredMenuActions.Clear();
            _menuActionsRegistered = false;
        }

        private void RegisterGameActions()
        {
            if (_gameActionsRegistered) return;
            _registeredGameActions.Clear();
            Log.Message($"[Neuro] Registering actions for '{NeuroRimMod.settings.selectedMode}' mode.");

            _registeredGameActions.Add(new PrioritizeWorkAction());
            _registeredGameActions.Add(new SetResearchProjectAction());
            _registeredGameActions.Add(new SetColonistDraftStatusAction());
            _registeredGameActions.Add(new ArmColonistsAction());
            _registeredGameActions.Add(new ArmIndividuallyAction());
            _registeredGameActions.Add(new FightAction());
            _registeredGameActions.Add(new ManageAnimalAction());
            _registeredGameActions.Add(new ForbidItemAction());

            if (NeuroRimMod.settings.selectedMode == NeuroMode.Storyteller)
            {
                _registeredGameActions.Add(new SpawnEventAction());
                _registeredGameActions.Add(new SpawnThingsAction());
                _registeredGameActions.Add(new SpawnPawnsAction());
                _registeredGameActions.Add(new ChangeWeatherAction());
                _registeredGameActions.Add(new DropPodRaidAction());
                _registeredGameActions.Add(new ForceMentalBreakAction());
                _registeredGameActions.Add(new ChangeFactionRelationsAction());
            }

            NeuroActionHandler.RegisterActions(_registeredGameActions);
            _gameActionsRegistered = true;
        }

        public void UnregisterGameActions()
        {
            if (!_gameActionsRegistered) return;
            Log.Message("[Neuro] Unregistering all game actions.");
            NeuroActionHandler.UnregisterActions(_registeredGameActions);
            _registeredGameActions.Clear();
            _gameActionsRegistered = false;
        }

        public void OnQuickStartExecuted()
        {
            var quickStartAction = _registeredMenuActions.FirstOrDefault(a => a is QuickStartAction);
            if (quickStartAction != null)
            {
                NeuroActionHandler.UnregisterActions(new List<INeuroAction> { quickStartAction });
                _registeredMenuActions.Remove(quickStartAction);
            }
        }

        public void RefreshAction<T>() where T : INeuroAction, new()
        {
            if (!_gameActionsRegistered) return;
            var oldAction = _registeredGameActions.FirstOrDefault(a => a is T);
            if (oldAction != null)
            {
                NeuroActionHandler.UnregisterActions(new List<INeuroAction> { oldAction });
                _registeredGameActions.Remove(oldAction);
                var newAction = new T();
                _registeredGameActions.Add(newAction);
                NeuroActionHandler.RegisterActions(new List<INeuroAction> { newAction });
                Log.Message($"[Neuro] Refreshed dynamic action: {newAction.Name}");
            }
        }
    }
}