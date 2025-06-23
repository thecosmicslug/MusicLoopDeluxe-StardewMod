using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicLoopDeluxe
{
    internal class ObjectPatches
    {
        //* initialized by ModEntry.cs
        public static IMonitor ModMonitor = null!;
        public static ModConfig Config = null!;

        //* parse comma-delimited lists ahead of time for speed
        public static List<string> TracksToIgnoreList = new();
        public static List<string> TrackNamesToReplaceWithIDList = new();

        //* track previous events to identify and skip repeat announcements
        private static string LastTrackAnnounced = "none";
        public static Dictionary<string, DateTime> WhenToReannounceTrack = new();

        public static void SetTracksToIgnore()
        {
            TracksToIgnoreList.Clear();
            foreach (var track in Config.TracksToIgnore.Split(','))
            {
                TracksToIgnoreList.Add(track.Trim());
            }
        }

        public static void SetTrackNamesToReplaceWithID()
        {
            TrackNamesToReplaceWithIDList.Clear();
            foreach (var trackName in Config.TrackNamesToReplaceWithID.Split(','))
            {
                TrackNamesToReplaceWithIDList.Add(trackName.Trim());
            }
        }

        public static void Game1_UpdateRequestedMusicTrack_Postfix()
        {
            try
            {
                //* e.g. "summer1"
                var songID = (Game1.currentLocation != null && Game1.currentLocation.IsMiniJukeboxPlaying())
                    ? Game1.currentLocation.miniJukeboxTrack.Value
                    : Game1.requestedMusicTrack;
                if (Config.DebugLogging){
                    ModMonitor.Log($"[Now Playing] Song ID = {songID}", LogLevel.Debug);
                }
                
                //* Are announcements currently disabled?
                if (Config.EnableAnnouncements == false)
                {   
                    if (Config.DebugLogging){
                        ModMonitor.Log($"[Now Playing] Announcements are disabled", LogLevel.Debug);
                    }
                    
                    //* Reset last track so it will announce it again if re-enabled.
                    LastTrackAnnounced = "none";
                    return;
                }

                //* Was this track already announced?
                if (songID == LastTrackAnnounced)
                {
                    if (Config.DebugLogging){
                        ModMonitor.Log($"[Now Playing] Already announced this track", LogLevel.Debug);
                    }
                    return;
                }
                LastTrackAnnounced = songID;

                //* Don't announce absence of a track
                if (songID == "none" || songID == "silence")
                {
                    if (Config.DebugLogging){
                        ModMonitor.Log($"[Now Playing] Skipping announcement of silence", LogLevel.Debug);
                    }
                    return;
                }

                //* Don't announce a track too soon after it was previously announced
                //* (occasionally the game requests e.g. both an ambient track and a music track multiple times within a short period of time,
                //* in which case the LastTrackAnnounced check doesn't catch it)
                if (WhenToReannounceTrack.ContainsKey(songID) && DateTime.Now < WhenToReannounceTrack[songID])
                {   
                    if (Config.DebugLogging){
                        ModMonitor.Log($"[Now Playing] Skipping announcement of {songID} too soon after it was previously announced", LogLevel.Debug);
                    }
                    return;
                }

                //* Don't announce an ignored track
                if (TracksToIgnoreList.Contains(songID))
                {
                    if (Config.DebugLogging){
                        ModMonitor.Log($"[Now Playing] Skipping announcement of {songID} by request", LogLevel.Debug);
                    }
                    return;
                }

                //* Get track title (custom or standard)
                var songTitle = (Config.TracksToRename.ContainsKey(songID))
                    ? Config.TracksToRename[songID]
                    : Utility.getSongTitleFromCueName(songID); // e.g. "Summer (Nature's Crescendo)"
                if (TrackNamesToReplaceWithIDList.Contains(songTitle))
                {
                    if (Config.DebugLogging){
                        ModMonitor.Log($"[Now Playing] Replacing {songTitle} with {songID} by request", LogLevel.Debug);
                    }
                    songTitle = songID;
                }
                if (Config.IgnoreUnnamedTracks && (songTitle == songID))
                {
                    if (Config.DebugLogging){
                        ModMonitor.Log($"[Now Playing] Skipping announcement of {songID} because title is same as ID", LogLevel.Debug);
                    }
                    return;
                }

                //* Announce new track
                if (Config.DebugLogging){
                    ModMonitor.Log($"[Now Playing] Song ID = {songID}, title = {songTitle}", LogLevel.Debug);
                }
                Game1.showGlobalMessage(string.Format(Config.NowPlayingFormat, songTitle, songID));

                //* Update cool-down timer for next announcement of same track
                WhenToReannounceTrack[songID] = DateTime.Now.AddSeconds(Config.RepeatDelay);
            }
            catch (Exception ex)
            {
                if (Config.DebugLogging){
                    ModMonitor.Log($"[Now Playing] Game1_UpdateRequestedMusicTrack_Postfix: {ex.Message} - {ex.StackTrace}", LogLevel.Error);
                }
            }
        }
    }
}
