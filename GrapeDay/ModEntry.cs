using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using StardewValley.Extensions;
using ContentPatcher;

namespace GrapeDay
{
    public class ModEntry : Mod
    {
        private const string ItemID_Grape = "398";
        private const string QualifiedItemID_Grape = "(O)398";

        /*********
        ** Properties
        *********/
        /// <summary>The mod configuration from the player.</summary>
        private ModConfig Config;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();

            Helper.Events.GameLoop.GameLaunched += (e, a) => OnGameLaunched(e, a);

            Helper.Events.GameLoop.DayEnding += (e, a) => SpawnGrapeDayObjects();
        }

        /// <summary>Initial setup</summary>
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            OnGameLaunched_GenericModConfigMenu();
            OnGameLaunched_ContentPatcher();
        }

        /// <summary>Add to Generic Mod Config Menu</summary>
        private void OnGameLaunched_GenericModConfigMenu() {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            // add config options
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => Helper.Translation.Get("Options_DayOfMonth_Name"),
                tooltip: () => Helper.Translation.Get("Options_DayOfMonth_Tooltip"),
                getValue: () => this.Config.DayOfMonth,
                setValue: value => this.Config.DayOfMonth = (int)value,
                min: 2,
                max: 28
            );
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => Helper.Translation.Get("Options_AdditionalSpawns_Name"),
                tooltip: () => Helper.Translation.Get("Options_AdditionalSpawns_Tooltip"),
                getValue: () => this.Config.AdditionalSpawns,
                setValue: value => this.Config.AdditionalSpawns = (int)value,
                min: 0,
                max: 10
            );
        }

        /// <summary>Add to Content Patcher</summary>
        private void OnGameLaunched_ContentPatcher()
        {
            // get Content Patcher's API (if it's installed)
            var contentPatcher = this.Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");
            if (contentPatcher == null)
            {
                return;
            }

            contentPatcher.RegisterToken(this.ModManifest, "DayOfMonth", () => {
                if (this.Config != null)
                {
                    return new[] { this.Config.DayOfMonth.ToString() };
                }
                return null;
            });
        }

        /// <summary>Spawn additional objects on Grape Day.</summary>
        private void SpawnGrapeDayObjects()
        {
            if (Game1.season != Season.Summer || Game1.dayOfMonth != Config.DayOfMonth - 1)
            {
                Monitor.Log($"[Grape Day] Tomorrow ({Game1.currentSeason.ToUpper()} {Game1.dayOfMonth + 1}) is not Grape Day (Summer {Config.DayOfMonth})", LogLevel.Trace);
                return;
            }

            if (Config.AdditionalSpawns < 1)
            {
                Monitor.Log($"[Grape Day] Number of additional grapes ({Config.AdditionalSpawns}) is too low", LogLevel.Trace);
                return;
            }

            // based on GameLocation.spawnObjects()

            var daySaveRandom = Utility.CreateDaySaveRandom();

            foreach (var location in Game1.locations)
            {
                var canSpawnGrapes = false;
                foreach (var forage in location.GetData().Forage)
                {
                    if (forage.Id == QualifiedItemID_Grape)
                    {
                        canSpawnGrapes = true;
                        break;
                    }
                }
                if (!canSpawnGrapes)
                {
                    Monitor.Log($"[Grape Day] Location {location.name} cannot spawn grapes", LogLevel.Trace);
                    continue;
                }

                var numberGrapesSpawned = 0;
                var maxAttempts = 100;
                for (var i = 0; i < maxAttempts && numberGrapesSpawned < Config.AdditionalSpawns; ++i)
                {
                    int x = daySaveRandom.Next(location.map.DisplayWidth / 64);
                    int y = daySaveRandom.Next(location.map.DisplayHeight / 64);
                    var vector2 = new Vector2((float)x, (float)y);
                    if (
                        (
                            location.objects.ContainsKey(vector2)
                                || location.IsNoSpawnTile(vector2)
                                || location.doesTileHaveProperty(x, y, "Spawnable", "Back") == null
                                || location.doesEitherTileOrTileIndexPropertyEqual(x, y, "Spawnable", "Back", "F")
                                || !location.CanItemBePlacedHere(vector2)
                                || location.hasTileAt(x, y, "AlwaysFront")
                                || location.hasTileAt(x, y, "AlwaysFront2")
                                || location.hasTileAt(x, y, "AlwaysFront3")
                                || location.hasTileAt(x, y, "Front")
                                || location.isBehindBush(vector2) ? 0 : (
                                    daySaveRandom.NextBool(0.1) ? 1 : (
                                        !location.isBehindTree(vector2) ? 1 : 0
                                    )
                                )
                        ) != 0)
                    {
                        Monitor.Log($"[Grape Day] Location {location.name} cannot spawn grape at X = {x}, Y = {y}", LogLevel.Trace);
                    }

                    var obj = new StardewValley.Object(itemId: ItemID_Grape, initialStack: 1);
                    if (location.dropObject(obj: obj, dropLocation: vector2 * 64f, viewport: Game1.viewport, initialPlacement: true))
                    {
                        Monitor.Log($"[Grape Day] Spawned grape in {location.name} at X = {x}, Y = {y}", LogLevel.Debug);
                        ++location.numberOfSpawnedObjectsOnMap;
                        ++numberGrapesSpawned;
                    }
                    else
                    {
                        Monitor.Log($"[Grape Day] Tried and failed to spawn grape in {location.name} at X = {x}, Y = {y}", LogLevel.Trace);
                    }

                    if (i == maxAttempts - 1 && numberGrapesSpawned < Config.AdditionalSpawns)
                    {
                        Monitor.Log($"[Grape Day] Max attempts reached for {location.name}, giving up", LogLevel.Debug);
                    }
                }
            }
        }
    }
}
