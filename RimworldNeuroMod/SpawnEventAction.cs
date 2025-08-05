#nullable enable

using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using RimWorld;
using RimWorld.QuestGen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace NeuroPlaysRimworld
{
    public class SpawnEventData
    {
        public string EventName = "";
        public string? Material;
    }

    public class SpawnEventAction : NeuroAction<SpawnEventData>
    {
        private static readonly Dictionary<string, IncidentDef> IncidentMap;
        private static readonly Dictionary<string, string> EventDescriptions;
        private static readonly HashSet<string> ThreatPointEvents;

        static SpawnEventAction()
        {
            IncidentMap = new Dictionary<string, IncidentDef>();
            EventDescriptions = new Dictionary<string, string>();
            ThreatPointEvents = new HashSet<string>();

            void AddIncident(string key, string description, string defName, bool isThreat = false)
            {
                var def = DefDatabase<IncidentDef>.GetNamed(defName, errorOnFail: false);
                if (def != null)
                {
                    IncidentMap[key] = def;
                    EventDescriptions[key] = description;
                    if (isThreat)
                    {
                        ThreatPointEvents.Add(key);
                    }
                }
            }

            AddIncident("raid_enemy", "Trigger a raid from a hostile faction.", "RaidEnemy", isThreat: true);
            AddIncident("infestation", "Cause an insect infestation to emerge from underground.", "Infestation");
            AddIncident("manhunter_pack", "Cause a pack of animals to go berserk.", "ManhunterPack", isThreat: true);
            AddIncident("flashstorm", "A violent thunderstorm with a high rate of lightning strikes, often causing fires.", "Flashstorm");
            AddIncident("solar_flare", "Disables all electrical devices for a day or two.", "SolarFlare");
            AddIncident("toxic_fallout", "Covers the map in a poisonous dust that sickens colonists and kills plants.", "ToxicFallout");
            AddIncident("volcanic_winter", "Spews ash into the atmosphere, lowering temperatures and blocking sunlight.", "VolcanicWinter");
            AddIncident("eclipse", "Temporarily blocks the sun, shutting down solar panels and slowing plant growth.", "Eclipse");
            AddIncident("heat_wave", "Causes a sudden, extreme shift to a higher temperature.", "HeatWave");
            AddIncident("cold_snap", "Causes a sudden, extreme shift to a lower temperature.", "ColdSnap");
            AddIncident("aurora", "A beautiful light show that provides a mood boost to colonists outdoors.", "Aurora");
            AddIncident("blight", "Causes a disease to spread rapidly through a specific crop type, destroying it.", "Blight");
            AddIncident("raid_friendly", "A friendly faction arrives to help in a fight.", "RaidFriendly");
            AddIncident("visitor_group", "A friendly group arrives to visit the colony.", "VisitorGroup");
            AddIncident("traveler_group", "A group of neutral travelers passes through the area.", "TravelerGroup");
            AddIncident("trade_caravan", "A friendly faction sends a trade caravan.", "TraderCaravanArrival");
            AddIncident("orbital_trader_arrival", "An orbital trade ship arrives in communications range.", "OrbitalTraderArrival");
            AddIncident("wanderer_join", "A random wanderer asks to join the colony.", "WandererJoin");
            AddIncident("farm_animals_wander_in", "A group of farm animals wanders in.", "FarmAnimalsWanderIn");
            AddIncident("herd_migration", "A large herd of animals migrates across the map.", "HerdMigration");
            AddIncident("ship_chunk_drop", "Drop deconstructable spaceship chunks.", "ShipChunkDrop");
            AddIncident("mech_cluster", "A hostile mechanoid cluster lands on the map.", "MechCluster", isThreat: true);
            AddIncident("wanderers_skylantern", "Wanderers arrive to release skylanterns, improving mood.", "WanderersSkylantern");
            AddIncident("gauranlen_pod_spawn", "A Gauranlen pod sprouts nearby.", "GauranlenPodSpawn");
            AddIncident("infestation_jelly", "An insect infestation that produces insect jelly emerges.", "Infestation_Jelly");
            AddIncident("noxious_haze", "A cloud of toxic gas envelops the map for a time.", "NoxiousHaze");
            AddIncident("harbinger_tree_spawn", "A terrifying harbinger tree appears.", "HarbingerTreeSpawn");
            AddIncident("frenzied_animals", "A group of animals becomes maddened and hostile.", "FrenziedAnimals", isThreat: true);
            AddIncident("pit_gate", "A mysterious pit to a dark dimension opens.", "PitGate");
            AddIncident("shambler_swarm", "A horde of shambling undead creatures attacks.", "ShamblerSwarm", isThreat: true);
            AddIncident("psychic_ritual_siege", "A group of psychic cultists lays siege to the colony.", "PsychicRitualSiege", isThreat: true);
            AddIncident("void_curiosity", "A strange manifestation of the void appears, tempting investigation.", "VoidCuriosity");
            AddIncident("drought", "An extended period of no rain, killing plants.", "Drought");
            AddIncident("lava_flow", "A slow-moving lava flow appears on the map edge.", "LavaFlow");

            EventDescriptions["give_quest"] = "Triggers a random quest opportunity.";
            EventDescriptions["meteorite"] = "Crash a meteorite made of a specific material onto the map.";
        }

        private static readonly List<string> MeteoriteMaterials = new List<string>
        {
            "Steel", "Plasteel", "Gold", "Silver", "Jade", "Uranium"
        };

        public SpawnEventAction() { }

        public override string Name => "spawn_event";

        protected override string Description => "Triggers a world event. Can be hostile, friendly, or a resource drop.";

        protected override JsonSchema Schema
        {
            get
            {
                var allEventNames = EventDescriptions.Keys.Cast<object>().ToList();
                return new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Required = new List<string> { "event_name" },
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["event_name"] = new JsonSchema { Enum = allEventNames },
                        ["material"] = new JsonSchema { Enum = MeteoriteMaterials.Cast<object>().ToList() }
                    }
                };
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out SpawnEventData? parsedData)
        {
            parsedData = null;
            string? eventName = actionData.Data?["event_name"]?.Value<string>();
            string? material = actionData.Data?["material"]?.Value<string>();

            if (string.IsNullOrEmpty(eventName))
            {
                return ExecutionResult.Failure("Missing required parameter: 'event_name'.");
            }

            if (!EventDescriptions.ContainsKey(eventName))
            {
                return ExecutionResult.Failure($"Invalid or unavailable event_name: '{eventName}'.");
            }

            if (eventName == "raid_enemy" && !Find.FactionManager.AllFactions.Any(f => f.HostileTo(Faction.OfPlayer) && !f.def.hidden))
            {
                return ExecutionResult.Failure("Action failed. Cannot trigger raid because no hostile factions exist.");
            }

            var friendlyEvents = new HashSet<string> { "visitor_group", "trade_caravan", "raid_friendly", "traveler_group" };
            if (friendlyEvents.Contains(eventName) && !Find.FactionManager.AllFactions.Any(f => !f.IsPlayer && !f.HostileTo(Faction.OfPlayer) && f.def.CanEverBeNonHostile))
            {
                return ExecutionResult.Failure("Action failed. No friendly or neutral factions are available for this event.");
            }

            if (eventName == "blight" && !Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.Plant).Any(p => p.def.plant is { Blightable: true } && p.Faction == Faction.OfPlayer))
            {
                return ExecutionResult.Failure("Action failed. Cannot trigger blight because there are no blightable player crops.");
            }

            if (eventName == "give_quest")
            {
                var map = Find.CurrentMap;
                float points = StorytellerUtility.DefaultThreatPointsNow(map);
                if (!DefDatabase<QuestScriptDef>.AllDefs.Any(q => q.IsRootRandomSelected && q.CanRun(points, map)))
                {
                    return ExecutionResult.Failure("Action failed. No available quests can be generated at this time.");
                }
            }

            if (eventName == "meteorite")
            {
                if (string.IsNullOrEmpty(material))
                {
                    return ExecutionResult.Failure("Missing required parameter 'material' for meteorite event.");
                }
                if (!MeteoriteMaterials.Contains(material!))
                {
                    return ExecutionResult.Failure($"Invalid material '{material}' for meteorite.");
                }
            }

            parsedData = new SpawnEventData { EventName = eventName!, Material = material };
            return ExecutionResult.Success($"Queuing event '{eventName}'.");
        }

        protected override UniTask ExecuteAsync(SpawnEventData? data)
        {
            if (data != null)
            {
                Log.Message($"[Neuro] Executing event: {data.EventName}");
                var map = Find.CurrentMap;
                var parms = new IncidentParms { target = map };

                bool success;
                if (data.EventName == "meteorite")
                {
                    success = SpawnMeteorite(map, data.Material);
                }
                else if (data.EventName == "give_quest")
                {
                    success = TryGenerateRandomQuest(map);
                }
                else
                {
                    success = FireIncident(data.EventName, parms);
                }

                if (!success)
                {
                    Log.Warning($"[Neuro] Failed to execute event: {data.EventName}. This may be due to game conditions or lack of a valid target.");
                }
            }
            return UniTask.CompletedTask;
        }

        private bool FireIncident(string eventName, IncidentParms parms)
        {
            if (!IncidentMap.TryGetValue(eventName, out IncidentDef incident))
            {
                Log.Error($"[Neuro] Could not find a matching IncidentDef for event: {eventName}. This should not happen.");
                return false;
            }

            if (ThreatPointEvents.Contains(eventName))
            {
                parms.points = StorytellerUtility.DefaultThreatPointsNow(parms.target);
            }

            if (eventName == "raid_enemy" && !TrySetFaction(parms, mustBeHostile: true)) return false;

            var friendlyEvents = new HashSet<string> { "visitor_group", "trade_caravan", "raid_friendly", "traveler_group" };
            if (friendlyEvents.Contains(eventName) && !TrySetFaction(parms, mustBeHostile: false)) return false;

            return incident.Worker.TryExecute(parms);
        }

        private bool TryGenerateRandomQuest(Map map)
        {
            var points = StorytellerUtility.DefaultThreatPointsNow(map);
            var slate = new Slate();
            slate.Set("points", points);

            QuestScriptDef? questScript = NaturalRandomQuestChooser.ChooseNaturalRandomQuest(points, map);

            if (questScript == null)
            {
                Log.Warning("[Neuro] give_quest failed: ChooseNaturalRandomQuest returned no quest. This may be due to cooldowns, wealth, population, or other unmet conditions.");
                return false;
            }

            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questScript, slate);
            if (quest == null)
            {
                Log.Error($"[Neuro] give_quest failed: QuestUtility.GenerateQuestAndMakeAvailable returned null for script '{questScript.defName}'.");
                return false;
            }

            if (!quest.hidden && quest.root.sendAvailableLetter)
            {
                QuestUtility.SendLetterQuestAvailable(quest);
            }

            Log.Message($"[Neuro] Successfully generated quest: {quest.name}");

            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"## ✅ Quest Generated: {quest.name}");
            messageBuilder.AppendLine();
            messageBuilder.AppendLine(quest.description);

            Context.Send(messageBuilder.ToString(), silent: true);
            return true;
        }

        private bool SpawnMeteorite(Map map, string? materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return false;
            ThingDef? resourceDef = DefDatabase<ThingDef>.GetNamed(materialName, false);
            if (resourceDef == null)
            {
                Log.Error($"[Neuro] Could not find ThingDef for material: {materialName}");
                return false;
            }

            ThingDef? mineableThingDef = DefDatabase<ThingDef>.AllDefsListForReading
              .FirstOrDefault(d => d.mineable && d.building?.mineableThing == resourceDef);

            if (mineableThingDef == null)
            {
                Log.Error($"[Neuro] Could not find a mineable rock for material: {materialName}");
                return false;
            }

            if (!CellFinder.TryFindRandomCellNear(map.Center, map, 20,
              c => c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && c.GetFirstPawn(map) == null,
              out IntVec3 spawnPos))
            {
                Log.Warning("[Neuro] Could not find a suitable location for meteorite spawn.");
                return false;
            }

            SkyfallerMaker.SpawnSkyfaller(ThingDefOf.MeteoriteIncoming, mineableThingDef, spawnPos, map);
            return true;
        }

        private bool TrySetFaction(IncidentParms parms, bool mustBeHostile)
        {
            var potentialFactions = Find.FactionManager.AllFactions.Where(f =>
                !f.IsPlayer &&
                !f.def.hidden &&
                (mustBeHostile ? f.HostileTo(Faction.OfPlayer) : !f.HostileTo(Faction.OfPlayer) && f.def.CanEverBeNonHostile)
            );

            if (potentialFactions.TryRandomElement(out Faction? result))
            {
                parms.faction = result;
                return true;
            }

            Log.Warning($"[Neuro] Could not find a suitable faction for event. (mustBeHostile={mustBeHostile})");
            return false;
        }
    }
}