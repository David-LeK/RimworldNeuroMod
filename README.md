# Neuro Plays Rimworld 🤖

This mod allows the AI VTuber **[Neuro-sama](https://twitch.tv/vedal987)** to take control of a RimWorld colony! It uses the official [Neuro Game SDK](https://github.com/VedalAI/neuro-game-sdk) to connect the game to Neuro, enabling her to monitor the colony's status and make decisions by executing various in-game actions.

**Author:** DavidLek

**Supported RimWorld Version:** 1.6

**Note**: this is tested in core game using [Tony](https://github.com/Pasu4/neuro-api-tony), **no DLC** required, if **DLC** is enabled, then Neuro can spawn events, items, or pawns in that DLC too.

**WIP**: new actions will be added in the future if I have time, PRs are welcome.

## ✨ Features

  * **Two Operation Modes**: Choose between **Storyteller Mode** for god-like powers (spawning raids, items, etc.) and **Player Mode** for a more standard gameplay experience (managing work, drafting, etc.).
  * **Colonist Management**: Neuro can manage colonist work priorities, set research projects, draft/undraft them, forbid items, and designate animals for hunting, taming, or slaughter.
  * **Combat Control**: She can arm colonists with the best available weapons or assign specific weapons to individuals. She can also order colonists to attack specific targets.
  * **Storyteller Powers**: In Storyteller mode, Neuro can trigger a wide variety of friendly, hostile, and environmental events, change the weather, spawn items and pawns, initiate drop pod raids, alter faction relations, and even force mental breaks or inspirations.
  * **Comprehensive Reporting**: The mod sends periodic, detailed reports about the colony's status to Neuro, keeping her informed about colonists, resources, power, research, and threats.
  * **Automatic Activation**: The mod's features activate automatically when a map is loaded and deactivate when returning to the main menu.

## 🔧 How It Works

The mod integrates the Neuro Game SDK into RimWorld. It establishes a **WebSocket connection** to a server that Neuro-sama is connected to.

1.  **Sending Data:** On a regular interval (every 1500 ticks, or ~25 seconds on normal speed), the mod gathers comprehensive data about the current game state, formats it into a markdown report, and sends it to Neuro as a **context message**.
2.  **Receiving Actions:** Neuro can execute any of the available "actions" at any time. When she decides to do something, the server sends an action message back to the mod.
3.  **Executing Actions:** The mod receives the message, validates the parameters, and executes the corresponding in-game function, whether it's changing a colonist's job, spawning a raid, or dropping a meteorite.

## 🚀 Installation

You can install this mod either by downloading a pre-packaged release or by building it from the source code.

### Method 1: From Release

1.  **Prerequisites:**
    * A legitimate copy of RimWorld (version 1.6).
    * The **[Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)** mod, which is a requirement for most RimWorld mods.

2.  **Download the Mod:**
    * Grab the latest `.zip` file from the release page.

3.  **Install the Mod:**
    * Unzip the downloaded file.
    * Place the resulting `NeuroPlaysRimworld` folder into your RimWorld `Mods` directory.

4.  **Activate in-game:**
    * Launch RimWorld.
    * Go to the **Mods** menu.
    * Enable **Harmony** and **Neuro Plays Rimworld**.
    * Make sure Harmony is loaded *before* this mod. Your mod load order should look like this:
        1.  Harmony
        2.  Core
        3.  Neuro Plays Rimworld
        4.  (Other mods...)
    * Restart RimWorld as prompted.

### Method 2: From Source

1.  **Prerequisites:**
    * [Visual Studio 2022](https://visualstudio.microsoft.com/) (with the ".NET desktop development" workload).
    * [.NET Framework 4.7.2 Targeting Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472).
    * A local copy of the RimWorld game.

2.  **Clone the Repository:**
    ```bash
    git clone https://github.com/DavidLek/RimworldNeuroMod.git
    cd RimworldNeuroMod/RimworldNeuroMod
    ```

3.  **Configure Project File:**
    * Open `RimworldNeuroMod.csproj` in a text editor.
    * Find the `<RimWorldPath>` property.
    * Change its value from `D:\RimWorld` to the full path of your RimWorld installation directory. This is crucial for the project to find the game's code libraries.
        ```xml
        <PropertyGroup>
            ...
            <RimWorldPath>C:\Program Files (x86)\Steam\steamapps\common\RimWorld</RimWorldPath> ...
        </PropertyGroup>
        ```

4.  **Build the Mod:**
    * Open `RimworldNeuroMod.csproj` in Visual Studio.
    * NuGet packages (like the Neuro SDK) should be restored automatically. You also need to get the Harmony dependency from [here](https://github.com/BepInEx/BepInEx/releases) or directly from the Harmony mod (`0Harmony.dll`).
    * Set the solution configuration to **Release**.
    * Build the project (Build > Build Solution, or `Ctrl+Shift+B`).
    * The build process is configured to automatically copy the compiled mod and all its dependencies directly into your RimWorld `Mods` folder.

5.  **Activate in-game:**
    * Follow Step 4 from the "From Release" instructions above to activate the mod in the game's menu.

## ⚙️ Configuration

To connect to Neuro, the mod needs a WebSocket URL. You can configure this in three ways, listed by priority:

1.  **Environment Variable (Highest Priority):**
    * This is the recommended method for streaming setups. Set an environment variable named `NEURO_SDK_WS_URL` to the provided WebSocket address. The mod will *always* use this if it's set.

2.  **In-Game Mod Settings (Medium Priority):**
    * Go to `Mods > Neuro Plays RimWorld > Advanced > Mod options`.
    * You can enter a custom WebSocket URL here. This is useful if you can't set an environment variable.
    * **A restart is required for changes to this setting to take effect.**

    

3.  **Default URL (Lowest Priority):**
    * If neither of the above is set, the mod will default to `ws://localhost:8000`. This is perfect for local testing with the **Randy** bot included in the Neuro Game SDK.

The mod will log which URL it is using in the RimWorld developer console (`~` key) when it starts up.

## 🧠 Neuro's Abilities (Actions)

### Player Actions (Available in Both Modes)

| Action Name | Description |
| :--- | :--- |
| **`set_work_priority`** | Sets the priority (0-4) of a specific job for any colonist. |
| **`set_research_project`** | Selects the colony's active research project from available technologies. |
| **`set_colonist_draft_status`**| Toggles a colonist's drafted mode on or off. |
| **`arm_colonists`** | Arms all capable, unarmed colonists with the best available weapons. |
| **`arm_individually`** | Orders a specific colonist to equip a specific weapon. |
| **`fight`** | Orders a colonist to attack a specific enemy or animal, drafting them if necessary. |
| **`manage_animal`** | Designates wild animals to be hunted/tamed, or colony animals for slaughter. |
| **`forbid_item`** | Forbids or unforbids a specific stack of items on the ground. |

### Storyteller-Only Actions

| Action Name | Description |
| :--- | :--- |
| **`spawn_event`** | Triggers a world event. Can be hostile, friendly, or a resource drop. |
| **`spawn_item`** | Spawns a specific item, resource, or building on the map. |
| **`spawn_pawn`** | Spawns a new pawn (animal or person), with an optional custom name. |
| **`change_weather`** | Instantly changes the current weather on the map. |
| **`drop_pod_raid_random`** | Initiates a drop pod raid with a chosen faction, size, and hostility. |
| **`force_mental_break`** | Triggers a specific mental break (or inspiration) on a chosen colonist. |
| **`change_faction_relations`**| Instantly alters the colony's relationship with a non-player faction. |

## 🔗 Dependencies

  * **[Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077):** Required for the mod to function.
  * **[VedalAI.NeuroSdk.Unity](https://github.com/VedalAI/neuro-game-sdk):** The Neuro Game SDK.

