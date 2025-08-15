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
        private bool _actionsRegistered = false;
        private readonly List<INeuroAction> _registeredActions = new List<INeuroAction>();

        private float contextTimer = 0f;
        private const float CONTEXT_UPDATE_INTERVAL = 1500f;

        public void GameUpdate()
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

            if (isMapReady && !_actionsRegistered)
            {
                RegisterAllActions();
            }
            else if (!isMapReady && _actionsRegistered)
            {
                UnregisterAllActions();
            }

            if (_actionsRegistered && !Find.TickManager.Paused)
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

        private void RegisterAllActions()
        {
            if (_actionsRegistered) return;
            Log.Message("[Neuro] Registering all available actions for Neuro.");
            _registeredActions.Clear();
            _registeredActions.Add(new PrioritizeWorkAction());
            _registeredActions.Add(new SpawnEventAction());
            _registeredActions.Add(new SpawnThingsAction());
            _registeredActions.Add(new SpawnPawnsAction());
            _registeredActions.Add(new ChangeWeatherAction());
            _registeredActions.Add(new DropPodRaidAction());
            _registeredActions.Add(new SetResearchProjectAction());
            _registeredActions.Add(new ForceMentalBreakAction());
            _registeredActions.Add(new SetColonistDraftStatusAction());
            _registeredActions.Add(new ArmColonistsAction());
            _registeredActions.Add(new ArmIndividuallyAction());

            NeuroActionHandler.RegisterActions(_registeredActions);
            _actionsRegistered = true;
        }

        private void UnregisterAllActions()
        {
            if (!_actionsRegistered) return;
            Log.Message("[Neuro] Unregistering all actions.");
            NeuroActionHandler.UnregisterActions(_registeredActions);
            _registeredActions.Clear();
            _actionsRegistered = false;
        }

        public void RefreshActions()
        {
            if (!_actionsRegistered) return;

            Log.Message("[Neuro] Action refresh.");
            UnregisterAllActions();
            RegisterAllActions();
        }

        public void RefreshAction<T>() where T : INeuroAction, new()
        {
            if (!_actionsRegistered) return;

            var oldAction = _registeredActions.FirstOrDefault(a => a is T);

            if (oldAction != null)
            {
                Log.Message($"[Neuro] Refreshing dynamic action: {oldAction.Name}");

                NeuroActionHandler.UnregisterActions(new List<INeuroAction> { oldAction });
                _registeredActions.Remove(oldAction);

                var newAction = new T();
                _registeredActions.Add(newAction);

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
                    string job = p.CurJob != null ? $"Doing: {p.GetJobReport()}" : "Idle";
                    sb.AppendLine($"- **{p.Name.ToStringShort}**: {job} ({health})");
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