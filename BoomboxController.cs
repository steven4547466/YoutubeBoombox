using BepInEx.Logging;
using BepInEx;
using LC_API.ServerAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using YoutubeDLSharp;
using static YoutubeBoombox.YoutubeBoombox;
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem.XR;
using System.Runtime.InteropServices;

namespace YoutubeBoombox
{
    public class BoomboxController
    {
        private static Dictionary<BoomboxItem, BoomboxController> controllers = new Dictionary<BoomboxItem, BoomboxController>();
        
        public static void Download(BoomboxItem boombox, string id, string type, bool broadcast = false)
        {
            BoomboxController controller = null;

            if (!controllers.ContainsKey(boombox))
            {
                controller = new BoomboxController(boombox);
                controllers.Add(boombox, controller);
            }
            else
            {
                controller = controllers[boombox];
            }

            controller.ProcessRequest(id, type, broadcast);
        }

        public static void AddReadyClient(BoomboxItem boombox, bool self = false)
        {
            BoomboxController controller = null;

            if (!controllers.ContainsKey(boombox))
            {
                controller = new BoomboxController(boombox);
                controllers.Add(boombox, controller);
            } 
            else
            {
                controller = controllers[boombox];
            }

            controller.AddReadyClient(self);
        }

        public static void SetIsPlaylist(BoomboxItem boombox, bool isPlaylist)
        {
            if (controllers.TryGetValue(boombox, out BoomboxController controller))
            {
                controller.IsPlaylist = isPlaylist;
            }
            else
            {
                BoomboxController newController = new BoomboxController(boombox);
                controller.IsPlaylist = isPlaylist;
                controllers.Add(boombox, newController);
            }
        }

        public static void SetCurrentUrl(BoomboxItem boombox, string url)
        {
            if (controllers.TryGetValue(boombox, out BoomboxController controller))
            {
                controller.CurrentUrl = url;
            }
            else
            {
                BoomboxController newController = new BoomboxController(boombox);
                controller.CurrentUrl = url;
                controllers.Add(boombox, newController);
            }
        }

        public static void SetCurrentId(BoomboxItem boombox, string id)
        {
            if (controllers.TryGetValue(boombox, out BoomboxController controller))
            {
                controller.CurrentId = id;
            }
            else
            {
                BoomboxController newController = new BoomboxController(boombox);
                controller.CurrentId = id;
                controllers.Add(boombox, newController);
            }
        }

        public static void ResetReadyClients(BoomboxItem boombox)
        {
            if (controllers.TryGetValue(boombox, out BoomboxController controller))
            {
                controller.ReadyClients = 0;
            }
        }

        public static void Clear()
        {
            controllers.Clear();
        }

        public BoomboxItem Boombox { get; private set; }

        public bool LocalPlayed { get; set; }

        public string CurrentUrl { get; set; }

        public string CurrentId { get; set; }

        public bool IsPlaylist { get; set; } = false;

        public int PlaylistCurrentIndex { get; set; } = 0;

        public int ReadyClients { get; set; } = 0;

        public BoomboxController(BoomboxItem boombox)
        {
            Boombox = boombox;
        }

        public void AddReadyClient(bool self = false)
        {
            ReadyClients++;

            if (self) Networking.Broadcast((int)Boombox.NetworkObjectId, NetworkingSignatures.BOOMBOX_READY_CLIENT_SIG);

            DebugLog($"READY CLIENT {ReadyClients}/{StartOfRound.Instance.connectedPlayersAmount + 1} | IS SELF?: {self}", EnableDebugLogs.Value);

            if (ReadyClients >= StartOfRound.Instance.connectedPlayersAmount + 1)
            {
                ReadyClients = 0;
                Boombox.boomboxAudio.loop = true;
                Boombox.isBeingUsed = true;
                Boombox.isPlayingMusic = true;
                Boombox.boomboxAudio.Play();

                if (IsPlaylist)
                {
                    Boombox.boomboxAudio.loop = false;
                    Boombox.StartCoroutine(PlaylistCoroutine());
                }
            }
        }

        public IEnumerator PlaylistCoroutine()
        {
            PrepareNextSongInPlaylist();
            while (Boombox.boomboxAudio.isPlaying)
            {
                yield return new WaitForSeconds(1);
            }
            IncrementPlaylistIndex();
        }

        public IEnumerator LoadSongCoroutine(string path)
        {
            if (PathsThisSession.Contains(path)) PathsThisSession.Remove(path);

            PathsThisSession.Insert(0, path);

            if (PathsThisSession.Count > MaxCachedDownloads.Value)
            {
                File.Delete(PathsThisSession[PathsThisSession.Count - 1]);
                PathsThisSession.RemoveAt(PathsThisSession.Count - 1);
            }

            string url = string.Format("file://{0}", path);
            WWW www = new WWW(url);
            yield return www;

            Boombox.boomboxAudio.clip = www.GetAudioClip(false, false);
            Boombox.boomboxAudio.pitch = 1f;

            YoutubeBoombox.DebugLog("BOOMBOX READY!", EnableDebugLogs.Value);

            AddReadyClient(true);
        }

        public void IncrementPlaylistIndex()
        {
            Boombox.boomboxAudio.Stop();

            PlaylistCurrentIndex++;

            ReadyClients = 0;

            if (InfoCache.PlaylistCache.TryGetValue(CurrentId, out List<string> videoIds))
            {
                if (PlaylistCurrentIndex < videoIds.Count)
                {
                    string id = videoIds[PlaylistCurrentIndex];
                    string url = $"https://youtube.com/watch?v={id}";
                    string newPath = Path.Combine(DownloadsPath, $"{id}.mp3");

                    DownloadVideo(id, url, newPath);
                }
            }
        }

        public void DownloadCurrentVideo(string newPath)
        {
            DownloadVideo(CurrentId, CurrentUrl, newPath);
        }

        public async void DownloadVideo(string id, string url, string newPath)
        {
            if (File.Exists(newPath))
            {
                Boombox.StartCoroutine(LoadSongCoroutine(newPath));

                return;
            }

            if (InfoCache.DurationCache.TryGetValue(id, out float duration))
            {
                if (duration > MaxSongDuration.Value)
                {
                    AddReadyClient(true);

                    return;
                }
            }
            else
            {
                var videoDataResult = await YoutubeBoombox.YoutubeDL.RunVideoDataFetch(url);

                if (videoDataResult.Success && videoDataResult.Data.Duration != null)
                {
                    InfoCache.DurationCache.Add(id, (float)videoDataResult.Data.Duration);
                    // Skip downloading videos that are too long
                    if (videoDataResult.Data.Duration > MaxSongDuration.Value)
                    {
                        AddReadyClient(true);

                        return;
                    }
                }
                else
                {
                    AddReadyClient(true);

                    return;
                }
            }

            var res = await YoutubeBoombox.YoutubeDL.RunAudioDownload(url, YoutubeDLSharp.Options.AudioConversionFormat.Mp3);

            if (res.Success)
            {
                YoutubeBoombox.DebugLog(res.Data, EnableDebugLogs.Value);
                YoutubeBoombox.DebugLog(newPath, EnableDebugLogs.Value);

                File.Move(res.Data, newPath);

                Boombox.StartCoroutine(LoadSongCoroutine(newPath));
            }
        }

        public void PrepareNextSongInPlaylist()
        {
            if (InfoCache.PlaylistCache.TryGetValue(CurrentId, out List<string> videoIds))
            {
                if (PlaylistCurrentIndex + 1 < videoIds.Count)
                {
                    string id = videoIds[PlaylistCurrentIndex + 1];
                    string url = $"https://youtube.com/watch?v={id}";
                    string newPath = Path.Combine(DownloadsPath, $"{id}.mp3");

                    PrepareSong(id, url, newPath);
                }
            }
        }

        public async void PrepareSong(string id, string url, string newPath)
        {
            if (File.Exists(newPath)) return;

            if (InfoCache.DurationCache.TryGetValue(id, out float duration))
            {
                if (duration > MaxSongDuration.Value) return;
            } 
            else
            {
                var videoDataResult = await YoutubeBoombox.YoutubeDL.RunVideoDataFetch(url);

                if (videoDataResult.Success && videoDataResult.Data.Duration != null)
                {
                    InfoCache.DurationCache.Add(id, (float)videoDataResult.Data.Duration);
                    // Skip preparing videos that are too long
                    if (videoDataResult.Data.Duration > MaxSongDuration.Value) return;
                }
                else
                {
                    return;
                }
            }

            var res = await YoutubeBoombox.YoutubeDL.RunAudioDownload(url, YoutubeDLSharp.Options.AudioConversionFormat.Mp3);

            if (res.Success) File.Move(res.Data, newPath);
        }

        public async void DownloadCurrentPlaylist()
        {
            PlaylistCurrentIndex = 0;
            if (!InfoCache.PlaylistCache.TryGetValue(CurrentId, out List<string> videoIds))
            {
                var playlistResult = await YoutubeBoombox.YoutubeDL.RunVideoPlaylistDownload(CurrentUrl, 1, null, null, "bestvideo+bestaudio/best",
                    YoutubeDLSharp.Options.VideoRecodeFormat.None, default, null, new InfoCache(CurrentId),
                    new YoutubeDLSharp.Options.OptionSet()
                    {
                        FlatPlaylist = true,
                        DumpJson = true
                    });

                if (!playlistResult.Success)
                {
                    AddReadyClient(Boombox, true);

                    return;
                }
                else
                {
                    videoIds = InfoCache.PlaylistCache[CurrentId];
                }
            }

            if (videoIds.Count == 0)
            {
                AddReadyClient(true);

                return;
            }

            string id = videoIds[0];
            string url = $"https://youtube.com/watch?v={id}";
            string newPath = Path.Combine(DownloadsPath, $"{id}.mp3");

            DownloadVideo(id, url, newPath);
        }

        public void ProcessRequest(string id, string type, bool broadcast = false)
        {
            if (broadcast)
            {
                LocalPlayed = true;
                Networking.Broadcast($"{id}|{type}|{Boombox.NetworkObjectId}", NetworkingSignatures.BOOMBOX_SIG);
            } 
            else
            {
                LocalPlayed = false;
            }

            string newPath = Path.Combine(DownloadsPath, $"{id}.mp3");

            string url = type == "video" ? $"https://youtube.com/watch?v={id}" : $"https://youtube.com/playlist?list={id}";

            CurrentUrl = url;
            IsPlaylist = type != "video";
            CurrentId = id;

            if (!IsPlaylist)
            {
                DownloadCurrentVideo(newPath);
            }
            else
            {
                DownloadCurrentPlaylist();
            }
        }
    }
}
