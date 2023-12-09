using LC_API.ServerAPI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using YoutubeDLSharp;
using static YoutubeBoombox.YoutubeBoombox;

namespace YoutubeBoombox
{
    public class BoomboxNetworking : NetworkBehaviour
    {
        private BoomboxItem Boombox { get; set; }

        private string CurrentId { get; set; }

        private string CurrentUrl { get; set; }

        private bool IsPlaylist { get; set; }

        private int PlaylistCurrentIndex { get; set; } = 0;

        private List<ulong> ReadyClients { get; set; } = new List<ulong>();

        public void Awake()
        {
            Boombox = GetComponent<BoomboxItem>();
        }

        [ServerRpc]
        public void DownloadServerRpc(string id, bool isPlaylist)
        {
            DebugLog($"Download server rpc received, sending to all", EnableDebugLogs.Value);
            DownloadClientRpc(id, isPlaylist);
        }

        [ClientRpc]
        public void DownloadClientRpc(string id, bool isPlaylist)
        {
            DebugLog($"Download request received on client, processing.", EnableDebugLogs.Value);
            ProcessRequest(id, isPlaylist);
        }

        public void Download(string id, bool isPlaylist)
        {
            DebugLog($"Download called, calling everywhere", EnableDebugLogs.Value);
            DownloadServerRpc(id, isPlaylist);
        }

        [ServerRpc(RequireOwnership = false)]
        public void IAmReadyServerRpc(ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer) return;

            ulong sender = serverRpcParams.Receive.SenderClientId;

            DebugLog($"Ready called from {sender}", EnableDebugLogs.Value);

            //ClientRpcParams clientRpcParams = new ClientRpcParams
            //{
            //    Send = new ClientRpcSendParams
            //    {
            //        TargetClientIds = NetworkManager.Singleton.ConnectedClientsIds.Where(id => id != sender).ToArray()
            //    }
            //};

            AddReadyClientRpc(sender);
        }

        [ClientRpc]
        public void AddReadyClientRpc(ulong readyId)
        {
            DebugLog($"READY CLIENT CALLED {ReadyClients.Contains(readyId)}", EnableDebugLogs.Value);
            if (ReadyClients.Contains(readyId)) return;

            ReadyClients.Add(readyId);

            DebugLog($"READY CLIENT {ReadyClients.Count}/{StartOfRound.Instance.connectedPlayersAmount + 1}", EnableDebugLogs.Value);

            if (ReadyClients.Count >= StartOfRound.Instance.connectedPlayersAmount + 1)
            {
                DebugLog($"Everyone ready, starting tunes!", EnableDebugLogs.Value);
                ReadyClients.Clear();
                Boombox.boomboxAudio.loop = true;
                Boombox.boomboxAudio.pitch = 1;
                Boombox.isBeingUsed = true;
                Boombox.isPlayingMusic = true;
                Boombox.boomboxAudio.Play();

                if (IsPlaylist)
                {
                    DebugLog($"Currently playing playlist, starting playlist routine.", EnableDebugLogs.Value);
                    Boombox.boomboxAudio.loop = false;
                    Boombox.StartCoroutine(PlaylistCoroutine());
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void StopMusicServerRpc(bool pitchDown)
        {
            StopMusicClientRpc(pitchDown);
        }

        [ClientRpc]
        public void StopMusicClientRpc(bool pitchDown)
        {
            if (pitchDown)
            {
                Boombox.StartCoroutine(Boombox.musicPitchDown());
            }
            else
            {
                Boombox.boomboxAudio.Stop();
                Boombox.boomboxAudio.PlayOneShot(Boombox.stopAudios[UnityEngine.Random.Range(0, Boombox.stopAudios.Length)]);
            }

            Boombox.timesPlayedWithoutTurningOff = 0;

            Boombox.isBeingUsed = false;
            Boombox.isPlayingMusic = false;
            ResetReadyClients();
        }

        public void ResetReadyClients()
        {
            ReadyClients.Clear();
        }

        public IEnumerator LoadSongCoroutine(string path)
        {
            DebugLog($"Loading song at {path}.", EnableDebugLogs.Value);

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

            DebugLog($"Successfully loaded song at {path}.", EnableDebugLogs.Value);

            Boombox.boomboxAudio.clip = www.GetAudioClip(false, false);

            DebugLog("BOOMBOX READY!", EnableDebugLogs.Value);

            IAmReadyServerRpc();
        }

        public void IncrementPlaylistIndex()
        {
            DebugLog($"Incrementing playlist index.", EnableDebugLogs.Value);

            Boombox.boomboxAudio.Stop();

            PlaylistCurrentIndex++;

            ReadyClients.Clear();

            if (InfoCache.PlaylistCache.TryGetValue(CurrentId, out List<string> videoIds))
            {
                if (PlaylistCurrentIndex < videoIds.Count)
                {
                    string id = videoIds[PlaylistCurrentIndex];
                    string url = $"https://youtube.com/watch?v={id}";
                    string newPath = Path.Combine(DownloadsPath, $"{id}.mp3");

                    DebugLog($"Downloading next playlist song.", EnableDebugLogs.Value);

                    DownloadSong(id, url, newPath);
                }
                else
                {
                    DebugLog($"Playlist complete!", EnableDebugLogs.Value);
                }
            } 
            else
            {
                DebugLog($"Playlist video ids not found! Cannot proceed!", EnableDebugLogs.Value);
                IAmReadyServerRpc();
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

        public void DownloadCurrentVideo(string newPath)
        {
            DebugLog($"Downloading {CurrentUrl} ({CurrentId})", EnableDebugLogs.Value);
            DownloadSong(CurrentId, CurrentUrl, newPath);
        }

        public async void DownloadSong(string id, string url, string newPath)
        {
            DebugLog($"Downloading song {url} ({id})", EnableDebugLogs.Value);

            if (File.Exists(newPath))
            {
                DebugLog($"File exists. Reusing.", EnableDebugLogs.Value);
                Boombox.StartCoroutine(LoadSongCoroutine(newPath));

                return;
            }

            if (InfoCache.DurationCache.TryGetValue(id, out float duration))
            {
                if (duration > MaxSongDuration.Value)
                {
                    DebugLog($"Song too long. Preventing download.", EnableDebugLogs.Value);
                    IAmReadyServerRpc();

                    return;
                }
            }
            else
            {
                DebugLog($"Downloading song duration data.", EnableDebugLogs.Value);
                var videoDataResult = await YoutubeBoombox.YoutubeDL.RunVideoDataFetch(url);

                if (videoDataResult.Success && videoDataResult.Data.Duration != null)
                {
                    InfoCache.DurationCache.Add(id, (float)videoDataResult.Data.Duration);
                    // Skip downloading videos that are too long
                    if (videoDataResult.Data.Duration > MaxSongDuration.Value)
                    {
                        DebugLog($"Song too long. Preventing download.", EnableDebugLogs.Value);
                        IAmReadyServerRpc();

                        return;
                    }
                }
                else
                {
                    DebugLog($"Couldn't get song data, skipping.", EnableDebugLogs.Value);
                    IAmReadyServerRpc();

                    return;
                }
            }

            var res = await YoutubeBoombox.YoutubeDL.RunAudioDownload(url, YoutubeDLSharp.Options.AudioConversionFormat.Mp3);

            if (res.Success)
            {
                DebugLog($"Song {id} downloaded successfully.", EnableDebugLogs.Value);

                File.Move(res.Data, newPath);

                Boombox.StartCoroutine(LoadSongCoroutine(newPath));
            }
            else
            {
                DebugLog($"Failed to download song {id}.", EnableDebugLogs.Value);
                IAmReadyServerRpc();
            }
        }

        public void PrepareNextSongInPlaylist()
        {
            DebugLog($"Preparing next song in playlist", EnableDebugLogs.Value);
            if (InfoCache.PlaylistCache.TryGetValue(CurrentId, out List<string> videoIds))
            {
                if (PlaylistCurrentIndex + 1 < videoIds.Count)
                {
                    string id = videoIds[PlaylistCurrentIndex + 1];
                    string url = $"https://youtube.com/watch?v={id}";
                    string newPath = Path.Combine(DownloadsPath, $"{id}.mp3");

                    DebugLog($"Preparing {url} ({id})", EnableDebugLogs.Value);

                    PrepareSong(id, url, newPath);
                }
                else
                {
                    DebugLog($"Playlist complete.", EnableDebugLogs.Value);
                }
            }
            else
            {
                DebugLog($"Couldn't find playlist ids!", EnableDebugLogs.Value);
            }
        }

        public async void PrepareSong(string id, string url, string newPath)
        {
            DebugLog($"Preparing next song {id}", EnableDebugLogs.Value);
            if (File.Exists(newPath))
            {
                DebugLog($"Already exists, reusing.", EnableDebugLogs.Value);
                return;
            }

            if (InfoCache.DurationCache.TryGetValue(id, out float duration))
            {
                if (duration > MaxSongDuration.Value)
                {
                    DebugLog($"Song too long. Preventing download.", EnableDebugLogs.Value);
                    return;
                }
            }
            else
            {
                var videoDataResult = await YoutubeBoombox.YoutubeDL.RunVideoDataFetch(url);

                if (videoDataResult.Success && videoDataResult.Data.Duration != null)
                {
                    InfoCache.DurationCache.Add(id, (float)videoDataResult.Data.Duration);
                    // Skip preparing videos that are too long
                    if (videoDataResult.Data.Duration > MaxSongDuration.Value)
                    {
                        DebugLog($"Song too long. Preventing download.", EnableDebugLogs.Value);
                        return;
                    }
                }
                else
                {
                    DebugLog($"Couldn't get song length. Skipping.", EnableDebugLogs.Value);
                    return;
                }
            }

            var res = await YoutubeBoombox.YoutubeDL.RunAudioDownload(url, YoutubeDLSharp.Options.AudioConversionFormat.Mp3);

            if (res.Success)
            {
                DebugLog($"Prepared {id} successfully", EnableDebugLogs.Value);
                File.Move(res.Data, newPath);
            }
            else
            {
                DebugLog($"Downloading {id} failed!", EnableDebugLogs.Value);
            }
        }

        public async void DownloadCurrentPlaylist()
        {
            DebugLog($"Downloading playlist from {CurrentUrl} ({CurrentId})", EnableDebugLogs.Value);

            PlaylistCurrentIndex = 0;
            if (!InfoCache.PlaylistCache.TryGetValue(CurrentId, out List<string> videoIds))
            {
                DebugLog($"Playlist not found in cache, downloading all ids.", EnableDebugLogs.Value);

                var playlistResult = await YoutubeBoombox.YoutubeDL.RunVideoPlaylistDownload(CurrentUrl, 1, null, null, "bestvideo+bestaudio/best",
                    YoutubeDLSharp.Options.VideoRecodeFormat.None, default, null, new InfoCache(CurrentId),
                    new YoutubeDLSharp.Options.OptionSet()
                    {
                        FlatPlaylist = true,
                        DumpJson = true
                    });

                if (!playlistResult.Success)
                {
                    DebugLog($"Failed to download playlist ids. Unable to proceed.", EnableDebugLogs.Value);
                    IAmReadyServerRpc();

                    return;
                }
                else
                {
                    videoIds = InfoCache.PlaylistCache[CurrentId];
                }
            }

            if (videoIds.Count == 0)
            {
                DebugLog($"Playlist video ids empty...", EnableDebugLogs.Value);
                IAmReadyServerRpc();

                return;
            }

            string id = videoIds[0];
            string url = $"https://youtube.com/watch?v={id}";
            string newPath = Path.Combine(DownloadsPath, $"{id}.mp3");

            DebugLog($"First playlist song found: {url} ({id})... Downloading.", EnableDebugLogs.Value);

            DownloadSong(id, url, newPath);
        }

        public void ProcessRequest(string id, bool isPlaylist)
        {
            string newPath = Path.Combine(DownloadsPath, $"{id}.mp3");

            string url = !isPlaylist ? $"https://youtube.com/watch?v={id}" : $"https://youtube.com/playlist?list={id}";

            CurrentUrl = url;
            IsPlaylist = isPlaylist;
            CurrentId = id;

            DebugLog($"Processing request for {id} isPlaylist?: {isPlaylist}", EnableDebugLogs.Value);

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
