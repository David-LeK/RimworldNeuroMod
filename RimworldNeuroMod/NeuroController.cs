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

        private float contextTimer = 0f;
        private const float CONTEXT_UPDATE_INTERVAL = 1500f;

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
                // In a game
                if (_menuActionsRegistered)
                {
                    UnregisterMenuActions();
                }
                if (!_gameActionsRegistered)
                {
                    RegisterGameActions();
                }

                // Context updates only happen in-game
                if (_gameActionsRegistered && !Find.TickManager.Paused)
                {
                    contextTimer += 1f;
                    if (contextTimer >= CONTEXT_UPDATE_INTERVAL)
                    {
                        contextTimer = 0f;
                        string gameStateSummary = GetGameStateAsText();
                        Context.Send(gameStateSummary, silent: true);
                        Log.Message("[Neuro] Sent periodic context update.");
                        RefreshActions();
                    }
                }
            }
            else
            {
                // Not in a game (e.g., main menu)
                if (_gameActionsRegistered)
                {
                    UnregisterGameActions();
                }
                if (!_menuActionsRegistered)
                {
                    RegisterMenuActions();
                }
            }
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

            // --- Player Actions (available in both modes) ---
            _registeredGameActions.Add(new PrioritizeWorkAction());
            _registeredGameActions.Add(new SetResearchProjectAction());
            _registeredGameActions.Add(new SetColonistDraftStatusAction());
            _registeredGameActions.Add(new ArmColonistsAction());
            _registeredGameActions.Add(new ArmIndividuallyAction());
            _registeredGameActions.Add(new FightAction());
            _registeredGameActions.Add(new ManageAnimalAction());
            _registeredGameActions.Add(new ForbidItemAction());

            // --- Storyteller-only Actions ---
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

        public void RefreshActions()
        {
            if (!_gameActionsRegistered) return;

            Log.Message("[Neuro] Action refresh.");
            UnregisterGameActions();
            RegisterGameActions();
        }

        public void RefreshAction<T>() where T : INeuroAction, new()
        {
            if (!_gameActionsRegistered) return;

            var oldAction = _registeredGameActions.FirstOrDefault(a => a is T);

            if (oldAction != null)
            {
                Log.Message($"[Neuro] Refreshing dynamic action: {oldAction.Name}");

                NeuroActionHandler.UnregisterActions(new List<INeuroAction> { oldAction });
                _registeredGameActions.Remove(oldAction);

                var newAction = new T();
                _registeredGameActions.Add(newAction);

                NeuroActionHandler.RegisterActions(new List<INeuroAction> { newAction });

                Log.Message($"[Neuro] Action '{newAction.Name}' has been refreshed successfully.");
            }
            else
            {
                Log.Warning($"[Neuro] Attempted to refresh an action of type '{typeof(T).Name}', but it was not found in the registered actions list.");
            }
        }

        private string GetGameStateAsText()
        {
            var map = Find.CurrentMap;
            if (map == null) return "No map loaded.";

            var sb = new StringBuilder();

            sb.AppendLine("## Colony Status Report");
            sb.AppendLine($"**Date:** {GenDate.DateFullStringAt(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile))}");
            sb.AppendLine($"**Colony Wealth:** ${map.wealthWatcher.WealthTotal:F0}");

            sb.AppendLine("\n## Colonists");
            var colonists = map.mapPawns.FreeColonists.OrderBy(p => p.Name.ToStringShort).ToList();
            if (colonists.Any())
            {
                foreach (var p in colonists)
                {
                    string health = $"Health: {(p.health.summaryHealth.SummaryHealthPercent * 100):F0}%";
                    string mood = p.needs?.mood != null ? $"Mood: {p.needs.mood.CurLevelPercentage * 100:F0}% (Break @ {p.mindState.mentalBreaker.BreakThresholdMinor * 100:F0}%)" : "No mood";
                    string job = p.CurJob != null ? p.GetJobReport().CapitalizeFirst() : "Idle";
                    sb.AppendLine($"- **{p.Name.ToStringShort}**: {job} ({health}, {mood})");
                }
            }
            else
            {
                sb.AppendLine("No colonists.");
            }

            sb.AppendLine("\n## Tamed Animals");
            var animals = map.mapPawns.PawnsInFaction(Faction.OfPlayer)
                .Where(p => p.RaceProps.Animal)
                .OrderBy(p => p.Label)
                .ToList();
            if (animals.Any())
            {
                foreach (var a in animals)
                {
                    sb.AppendLine($"- {a.LabelShortCap} ({a.GetGenderLabel()})");
                }
            }
            else
            {
                sb.AppendLine("No tamed animals.");
            }

            sb.AppendLine("\n## Prisoners");
            var prisoners = map.mapPawns.PrisonersOfColony.OrderBy(p => p.Name.ToStringShort).ToList();
            if (prisoners.Any())
            {
                foreach (var p in prisoners)
                {
                    string difficulty = $"Resistance: {p.guest.Resistance:F1}";
                    var injuries = p.health.hediffSet.hediffs
                        .OfType<Hediff_Injury>()
                        .GroupBy(h => h.Label)
                        .Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key)
                        .ToList();
                    string injuryReport = injuries.Any() ? $"Injuries: {string.Join(", ", injuries)}" : "No major injuries";
                    sb.AppendLine($"- **{p.Name.ToStringShort}**: ({difficulty}). {injuryReport}");
                }
            }
            else
            {
                sb.AppendLine("No prisoners.");
            }

            sb.AppendLine("\n## Resources");
            map.resourceCounter.UpdateResourceCounts();
            var resources = map.resourceCounter.AllCountedAmounts;

            void AppendKeyResource(string label, ThingDef? def)
            {
                if (def != null)
                {
                    sb.AppendLine($"- **{label}:** {resources.GetValueOrDefault(def, 0)}");
                }
            }

            var hardcodedDefs = new HashSet<ThingDef?>();
            void TrackAndAppend(string label, ThingDef? def)
            {
                AppendKeyResource(label, def);
                hardcodedDefs.Add(def);
            }

            TrackAndAppend("Simple Meals", ThingDefOf.MealSimple);
            TrackAndAppend("Medicine", ThingDefOf.MedicineIndustrial);
            TrackAndAppend("Wood", ThingDefOf.WoodLog);
            TrackAndAppend("Steel", ThingDefOf.Steel);
            TrackAndAppend("Components", ThingDefOf.ComponentIndustrial);
            TrackAndAppend("Silver", ThingDefOf.Silver);

            sb.AppendLine("---");

            var otherResources = resources
                .Where(kvp => kvp.Value > 0 && kvp.Key.CountAsResource && kvp.Key.BaseMarketValue > 0.1f && !hardcodedDefs.Contains(kvp.Key))
                .OrderByDescending(kvp => kvp.Value * kvp.Key.BaseMarketValue)
                .Take(10)
                .ToList();

            if (otherResources.Any())
            {
                sb.AppendLine("**Other Notable Stacks:**");
                foreach (var item in otherResources)
                {
                    sb.AppendLine($"- {item.Key.LabelCap}: {item.Value}");
                }
            }

            sb.AppendLine("\n## Power Grid");
            float totalPowerProduced = 0f;
            float totalPowerConsumed = 0f;
            if (map.powerNetManager != null)
            {
                foreach (var net in map.powerNetManager.AllNetsListForReading)
                {
                    foreach (var comp in net.powerComps)
                    {
                        if (comp.PowerOutput > 0)
                        {
                            totalPowerProduced += comp.PowerOutput;
                        }
                        else
                        {
                            totalPowerConsumed += comp.PowerOutput;
                        }
                    }
                    float balance = net.CurrentEnergyGainRate();
                    foreach (var battery in net.batteryComps)
                    {
                        if (balance < 0)
                        {
                            totalPowerProduced += battery.StoredEnergy;
                        }
                        else
                        {
                            totalPowerConsumed -= balance * battery.Props.efficiency;
                        }
                    }
                }
            }
            float powerDelta = totalPowerProduced + totalPowerConsumed;
            sb.AppendLine($"- **Produced:** {totalPowerProduced:F0} W");
            sb.AppendLine($"- **Consumed:** {Mathf.Abs(totalPowerConsumed):F0} W");
            sb.AppendLine($"- **Net:** {powerDelta:F0} W");
            sb.AppendLine(powerDelta < 0 ? "- **Status:** Brownout / Blackout" : "- **Status:** Stable");

            sb.AppendLine("\n## Research");
            var currentProj = Find.ResearchManager.GetProject();
            if (currentProj != null)
            {
                sb.AppendLine($"**Active Project:** {currentProj.label} ({(Find.ResearchManager.GetProgress(currentProj) / currentProj.baseCost):P0} complete)");
            }
            else
            {
                sb.AppendLine("**Active Project:** None");
            }

            var availableProjects = DefDatabase<ResearchProjectDef>.AllDefs
                .Where(p => p.CanStartNow && p != currentProj && Find.ResearchManager.GetProgress(p) > 0)
                .OrderBy(p => p.label)
                .ToList();

            if (availableProjects.Any())
            {
                sb.AppendLine("\n**Available Projects:**");
                foreach (var proj in availableProjects)
                {
                    float progress = Find.ResearchManager.GetProgress(proj);
                    sb.AppendLine($"- {proj.label} ({(progress / proj.baseCost):P0})");
                }
            }

            sb.AppendLine("\n## Current Threats");
            var threats = map.attackTargetsCache.TargetsHostileToColony
                .Select(t => t.Thing)
                .Where(t => t is Pawn p && !p.Downed)
                .GroupBy(p => p.def.label)
                .Select(g => new { Name = g.Key, Count = g.Count() });

            if (threats.Any())
            {
                foreach (var threat in threats)
                {
                    sb.AppendLine($"- {threat.Count}x {threat.Name}");
                }
            }
            else
            {
                sb.AppendLine("None detected.");
            }

            return sb.ToString();
        }
    }
}