//* Orinally Based on SoundLoopMod (https://git.strelkasaurus.com/strelkasaurus/stardew-soundloop-mod)
//* PauseInMenu option based on https://github.com/mustafa-git/StopSoundsWhenAltTabbed
//* Now playing option from https://github.com/emurphy42/NowPlaying
using System;
using System.Globalization;
using System.Reflection;

using StardewValley;
using StardewValley.Menus;
using StardewValley.Locations;
using StardewValley.Objects;

using StardewModdingAPI;
using StardewModdingAPI.Events;

﻿using GenericModConfigMenu;
using HarmonyLib;

namespace MusicLoopDeluxe
{
    public class ModEntry : Mod{

        //* Store current settings to restore later.
        private float musicVol;
        private float soundVol;
        private float ambientVol;
        private float footstepVol;
        private bool volumeSaved = false;
        private ModConfig Config = null!;
        private const string WoodsName = "Woods";


        public override void Entry(IModHelper helper){

            //* Setup Hooks
            helper.Events.GameLoop.UpdateTicking += this.OnUpdateTicking;
            helper.Events.GameLoop.OneSecondUpdateTicking += this.OnSecondUpdateTicking;
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Player.Warped += this.OnPlayerWarped;
            helper.Events.Input.ButtonPressed  +=  this.OnButtonPressed;
            
            //* Load Config
            this.Config = this.Helper.ReadConfig<ModConfig>();

            //* Show debug info etc.
            var buildTime = GetBuildDate(Assembly.GetExecutingAssembly());
            buildTime = buildTime.ToLocalTime();
            this.Monitor.Log("MusicLoopDeluxe v" + GetType().Assembly.GetName().Version.ToString(3) +" (" + Constants.TargetPlatform + ") loaded.", LogLevel.Info);
            if(this.Config.DebugLogging){
                this.Monitor.Log("Binary Compiled: " + buildTime.ToString("d/M/yyyy h:mm tt"), LogLevel.Info);
            }

            //* Setup Harmony Patch
            ObjectPatches.ModMonitor = this.Monitor;
            ObjectPatches.Config = this.Config;
            ObjectPatches.SetTracksToIgnore();
            ObjectPatches.SetTrackNamesToReplaceWithID();

            var harmony = new Harmony(this.ModManifest.UniqueID);
            //* detect when music changes
            harmony.Patch(
               original: AccessTools.Method(typeof(StardewValley.Game1), nameof(StardewValley.Game1.UpdateRequestedMusicTrack)),
               postfix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.Game1_UpdateRequestedMusicTrack_Postfix))
            );
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            //* get Generic Mod Config Menu's API (if it's installed)
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            //* register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            //* parse comma-delimited lists ahead of time for speed
            configMenu.OnFieldChanged(
                mod: this.ModManifest,
                onChange: (fieldID, fieldValue) => this.OnFieldChanged(fieldID, fieldValue)
            );

            //* add config options
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Pause music in menus",
                tooltip: () => "Music will be paused while in the game menus.",
                getValue: () => this.Config.PauseInMenu,
                setValue: value => this.Config.PauseInMenu = value
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Loop background music",
                tooltip: () => "Loop music to prevent silence.",
                getValue: () => this.Config.LoopMusic,
                setValue: value => this.Config.LoopMusic = value
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Debug Logging",
                tooltip: () => "Print info to SMAPI log for debugging.",
                getValue: () => this.Config.DebugLogging,
                setValue: value => this.Config.DebugLogging = value
            );

            //* Future improvement: increase width of "Message format" input box
            configMenu.AddTextOption(
                mod: this.ModManifest,
                name: () => "Message format",
                getValue: () => this.Config.NowPlayingFormat,
                setValue: value => this.Config.NowPlayingFormat = value
            );
            configMenu.AddTextOption(
                mod: this.ModManifest,
                name: () => "Tracks to ignore",
                tooltip: () => "Track IDs separated by commas",
                getValue: () => this.Config.TracksToIgnore,
                setValue: value => this.Config.TracksToIgnore = value
            );
            configMenu.AddTextOption(
                mod: this.ModManifest,
                name: () => "Track names to replace with ID",
                tooltip: () => "Track names separated by commas",
                getValue: () => this.Config.TrackNamesToReplaceWithID,
                setValue: value => this.Config.TrackNamesToReplaceWithID = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Ignore unnamed tracks",
                tooltip: () => "Skip announcing track if name is same as ID",
                getValue: () => this.Config.IgnoreUnnamedTracks,
                setValue: value => this.Config.IgnoreUnnamedTracks = value
            );
            // Future improvement: TracksToRename
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Repeat delay",
                tooltip: () => "Wait this many seconds before re-announcing a track",
                getValue: () => this.Config.RepeatDelay,
                setValue: value => this.Config.RepeatDelay = value
            );
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Enable announcements",
                getValue: () => this.Config.EnableAnnouncements,
                setValue: value => this.Config.EnableAnnouncements = value
            );
            configMenu.AddKeybind(
                mod: this.ModManifest,
                name: () => "Toggle announcements",
                tooltip: () => "Enables or disables announcements",
                getValue: () => this.Config.ToggleAnnouncementsKey,
                setValue: value => this.Config.ToggleAnnouncementsKey = value
            );

        }

        void OnSecondUpdateTicking(object? sender, OneSecondUpdateTickingEventArgs e){

            //* Don't do anything if we're at the title screen
            if (!Context.IsWorldReady){
                return;
            }
                
            if (!Config.LoopMusic){
                return;
            }
            
            //* Music won't play again in the mines if this timestamp is lower than 150000. Set to some arbitrary large value.
            //* The skull cavern is considered an extension of the mines, so this should affect that as well.
            MineShaft.timeSinceLastMusic = 999999;
            
            //* Avoid playing morning song in the mines etc.
            if (!Game1.currentLocation.IsOutdoors)
                return;
        
            //* The game keeps trying to stop the music if it's dark. We won't fight it for now
            if (Game1.isDarkOut(Game1.currentLocation))
                return;
            
            //* Don't start music while on the bus
            //* shouldHideCharacters seems to only be used for the bus, seems to be the nicest way to get this info
            if (Game1.currentLocation.shouldHideCharacters())
                return;
            
            //* Be extra-safe about not doing anything during an event (to protect the special cases below)
            if (Game1.eventUp)
                return;
        
            //* Seems to be safe to touch the current song, check if it has finished
            if (Game1.currentSong == null || Game1.currentSong.IsStopped || Game1.requestedMusicTrack.ToLower().Contains("ambient"))
            {
                if (Game1.currentLocation.Name == WoodsName)
                {
                    //* The music for the secret woods is a bit of a special case.
                    Game1.changeMusicTrack("woodsTheme");
                }
                else
                {
                    //* General case, let the game figure out what song to play.
                    Game1.playMorningSong();
                }

                if (Config.DebugLogging){
                    this.Monitor.Log("[MusicLoop] Restarted music");
                }
                
            }

        }

        void OnPlayerWarped(object? sender, WarpedEventArgs e){
            //* The game won't touch the music when entering the woods after 1800, causing the previously playing
            //* music to keep playing. Hence we stop it and let CheckMusicNeedsRestarting play the woods theme.
            //* However, after dark, let it keep playing the ambient night sound.
            if (!Config.LoopMusic){
                return;
            }
            if (e.IsLocalPlayer && e.NewLocation.Name == WoodsName && !Game1.isDarkOut(Game1.currentLocation))
            {
                Game1.changeMusicTrack("none");
            }
        }

        private void OnFieldChanged(string fieldID, object fieldValue)
        {
            //* Update our Configuration.
            switch (fieldID)
            {
                case "TracksToIgnore":
                    ObjectPatches.SetTracksToIgnore();
                    break;
                case "TrackNamesToReplaceWithID":
                    ObjectPatches.SetTrackNamesToReplaceWithID();
                    break;
            }
        }

        ///* <summary>React to toggle key</summary>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (e.Button == this.Config.ToggleAnnouncementsKey)
            {
                this.Config.EnableAnnouncements = !this.Config.EnableAnnouncements;
                var enabledDescription = this.Config.EnableAnnouncements ? "enabled" : "disabled";
                if (Config.DebugLogging){
                    this.Monitor.Log($"[Now Playing] Announcements are now {enabledDescription}", LogLevel.Debug);
                }
            }
        }

        void OnUpdateTicking(object? sender, UpdateTickingEventArgs e){

            if (!Context.IsWorldReady)
                return;

            if (!Context.IsGameLaunched || Game1.game1 is null){
                return;
            }

            ModCheckMenu();
        }

        void ModCheckMenu(){

            //* Mute sounds in menus
            if (Game1.activeClickableMenu is GameMenu){
                if (!volumeSaved){

                    if (!Config.PauseInMenu){
                        return;
                    }

                    soundVol = Game1.options.soundVolumeLevel;
                    ambientVol = Game1.options.ambientVolumeLevel;
                    footstepVol = Game1.options.footstepVolumeLevel;
                    musicVol = Game1.options.musicVolumeLevel;

                    //* Work around for the mini-games.
                    if (Game1.currentSong is null){
                        //* Do Nothing
                    }else{
                        Game1.currentSong.Pause();
                    }
                    
                    Game1.musicCategory.SetVolume(0.0f);
                    Game1.musicPlayerVolume = 0.0f;
                    Game1.soundCategory.SetVolume(0.0f);
                    Game1.ambientCategory.SetVolume(0.0f);
                    Game1.ambientPlayerVolume = 0.0f;
                    Game1.footstepCategory.SetVolume(0.0f);
                    volumeSaved = true;
                }
            //* Restore Sounds
            }else{

                if (volumeSaved){
                    Game1.currentSong.Resume();
                    Game1.soundCategory.SetVolume(soundVol);
                    Game1.musicCategory.SetVolume(musicVol);
                    Game1.musicPlayerVolume = musicVol;
                    Game1.ambientCategory.SetVolume(ambientVol);
                    Game1.ambientPlayerVolume = ambientVol;
                    Game1.footstepCategory.SetVolume(footstepVol);
                    volumeSaved = false;
                }

            }

        }

        private DateTime GetBuildDate(Assembly assembly){

            const string BuildVersionMetadataPrefix = "+build";
            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute?.InformationalVersion != null){
                var value = attribute.InformationalVersion;
                var index = value.IndexOf(BuildVersionMetadataPrefix);
                if (index > 0){
                    value = value.Substring(index + BuildVersionMetadataPrefix.Length);
                    if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)){
                        return result;
                    }
                }
            }
            return default;
        }

    }
}
