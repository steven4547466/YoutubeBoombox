using BepInEx;
using HarmonyLib;
using YoutubeDLSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using LC_API.ServerAPI;
using GameNetcodeStuff;
using Unity.Netcode;
using System.Reflection.Emit;
using UnityEngine.Pool;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace YoutubeBoombox
{
    [BepInPlugin("steven4547466.YoutubeBoombox", "Youtube Boombox", "1.1.1")]
    public class YoutubeBoombox : BaseUnityPlugin
    {
        private static Harmony Harmony { get; set; }

        internal static string DirectoryPath { get; private set; } = Path.Combine(Directory.GetCurrentDirectory(), "Youtube-Boombox");

        internal static string DownloadsPath { get; private set; } = Path.Combine(DirectoryPath, "Downloads");

        internal static YoutubeBoombox Singleton { get; private set; }

        public static YoutubeDL YoutubeDL { get; private set; } = new YoutubeDL();

        public static void Log(object data, BepInEx.Logging.LogLevel level = BepInEx.Logging.LogLevel.Info)
        {
            Singleton.Logger.Log(level, data);
        }

        async void Awake()
        {
            Singleton = this;

            if (!Directory.Exists(DirectoryPath)) Directory.CreateDirectory(DirectoryPath);
            if (!Directory.Exists(DownloadsPath)) Directory.CreateDirectory(DownloadsPath);

            foreach (string file in Directory.GetFiles(DownloadsPath))
            {
                File.Delete(file);
            }

            if (!Directory.GetFiles(DirectoryPath).Any(file => file.Contains("yt-dl"))) await Utils.DownloadYtDlp(DirectoryPath);
            if (!Directory.GetFiles(DirectoryPath).Any(file => file.Contains("ffmpeg"))) await Utils.DownloadFFmpeg(DirectoryPath);

            YoutubeDL.YoutubeDLPath = Directory.GetFiles(DirectoryPath).First(file => file.Contains("yt-dl"));
            YoutubeDL.FFmpegPath = Directory.GetFiles(DirectoryPath).First(file => file.Contains("ffmpeg"));

            YoutubeDL.OutputFolder = DownloadsPath;

            Harmony = new Harmony($"steven4547466.YoutubeBoombox-{DateTime.Now.Ticks}");

            Harmony.PatchAll();

            SetupNetworking();

            CommandHandler.CommandHandler.RegisterCommand("bbv", new List<string>() { "boomboxvolume" }, (string[] args) =>
            {
                if (args.Length > 0 && float.TryParse(args[0], out float volume))
                {
                    if (StartOfRound.Instance.localPlayerController.currentlyHeldObjectServer is BoomboxItem boombox)
                    {
                        boombox.boomboxAudio.volume = volume * 100;
                    }
                }
            });
        }

        private void SetupNetworking()
        {
            Networking.GetString += GetNetworkStringBroadcast;
            Networking.GetInt += GetNetworkIntBroadcast;
        }

        private void GetNetworkStringBroadcast(string data, string signature)
        {
            //Logger.LogInfo($"GOT STRING BROADCAST {data}|{signature}");
            if (signature == NetworkingSignatures.BOOMBOX_SIG)
            {
                string[] split = data.Split('|');
                string videoId = split[0];
                ulong netId;

                if (!ulong.TryParse(split[1], out netId))
                {
                    Logger.LogError("Unable to find boombox id in data");

                    return;
                }

                BoomboxItem boombox = FindObjectsOfType<BoomboxItem>().FirstOrDefault(b => b.NetworkObjectId == netId);

                if (!boombox)
                {
                    Logger.LogError($"Unable to find boombox with net id: {netId}");

                    return;
                }

                BoomboxPatch.CurrentBoombox = boombox;

                Download($"https://youtube.com/watch?v={videoId}", boombox);
            }
            else if (signature == NetworkingSignatures.BOOMBOX_OFF_SIG)
            {
                string[] split = data.Split('|');

                ulong netId;

                if (!ulong.TryParse(split[0], out netId))
                {
                    Logger.LogError("Unable to find boombox id in data");

                    return;
                }

                bool pitchDown;
                if (!bool.TryParse(split[1], out pitchDown))
                {
                    Logger.LogError("Unable to find pitchDown in data");

                    return;
                }

                BoomboxItem boombox = FindObjectsOfType<BoomboxItem>().FirstOrDefault(b => b.NetworkObjectId == netId);

                if (!boombox)
                {
                    Logger.LogError($"Unable to find boombox with net id: {netId}");

                    return;
                }

                if (pitchDown)
                {
                    boombox.StartCoroutine(boombox.musicPitchDown());
                }
                else
                {
                    boombox.boomboxAudio.Stop();
                    boombox.boomboxAudio.PlayOneShot(boombox.stopAudios[UnityEngine.Random.Range(0, boombox.stopAudios.Length)]);
                }

                boombox.timesPlayedWithoutTurningOff = 0;

                boombox.isBeingUsed = false;
                boombox.isPlayingMusic = false;
                ClientTracker.Reset(boombox);
            }
        }

        private void GetNetworkIntBroadcast(int data, string signature)
        {
            //Logger.LogInfo($"GOT INT BROADCAST {data}|{signature}");
            if (signature == NetworkingSignatures.BOOMBOX_READY_CLIENT_SIG)
            {
                ulong netId = (ulong)data;
                BoomboxItem boombox = FindObjectsOfType<BoomboxItem>().FirstOrDefault(b => b.NetworkObjectId == netId);

                if (!boombox)
                {
                    Logger.LogError($"Unable to find boombox with net id: {netId}");

                    return;
                }

                ClientTracker.AddReadyClient(boombox);
            }
        }

        static IEnumerator LoadSongCoroutine(BoomboxItem boombox, string path)
        {
            //Singleton.Logger.LogInfo("Loading song");
            string url = string.Format("file://{0}", path);
            WWW www = new WWW(url);
            yield return www;

            boombox.boomboxAudio.clip = www.GetAudioClip(false, false);
            boombox.boomboxAudio.pitch = 1f;

            //Singleton.Logger.LogInfo("BOOMBOX READY!");

            ClientTracker.AddReadyClient(boombox, true);
        }

        static async void Download(string url, BoomboxItem boombox, bool broadcast = false)
        {
            //Singleton.Logger.LogInfo($"Downloading song {url}");

            string videoId;

            if (url.Contains("?v="))
            {
                videoId = url.Split(new[] { "?v=" }, StringSplitOptions.None)[1];
            } 
            else if (url.Contains("youtu.be"))
            {
                if (url.EndsWith("/")) url = url.Substring(0, url.Length - 1);

                string[] split = url.Split('/');

                videoId = split[split.Length - 1];
            }
            else
            {
                Singleton.Logger.LogError("Couldn't resolve URL.");
                return;
            }

            if (broadcast) Networking.Broadcast($"{videoId}|{boombox.NetworkObjectId}", NetworkingSignatures.BOOMBOX_SIG);

            string newPath = Path.Combine(DownloadsPath, $"{videoId}.mp3");

            if (File.Exists(newPath))
            {
                boombox.StartCoroutine(LoadSongCoroutine(boombox, newPath));
            }
            else
            {
                var res = await YoutubeDL.RunAudioDownload(url, YoutubeDLSharp.Options.AudioConversionFormat.Mp3);

                if (res.Success)
                {
                    //Singleton.Logger.LogInfo(res.Data);
                    //Singleton.Logger.LogInfo(newPath);

                    File.Move(res.Data, newPath);

                    boombox.StartCoroutine(LoadSongCoroutine(boombox, newPath));
                }
            }
        }

        public static void PlaySong(string url)
        {
            //Singleton.Logger.LogInfo($"Trying to play {url}");
            if (BoomboxPatch.CurrentBoombox == null) return;

            //Singleton.Logger.LogInfo("Boombox found");

            Download(url, BoomboxPatch.CurrentBoombox, true);
        }

        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.DiscardItemOnClient))]
        class DropItemPatch
        {
            public static void Prefix(GrabbableObject __instance)
            {
                if (!__instance.IsOwner)
                {
                    return;
                }

                if (BoomboxPatch.ShowingGUI)
                {
                    BoomboxPatch.ShowingGUI = false;
                }
            }
        }

        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.UseItemOnClient))]
        class PreventRpc
        {
            public static bool IsBoombox(GrabbableObject obj)
            {
                return obj is BoomboxItem;
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                List<CodeInstruction> newInstructions = new List<CodeInstruction>(instructions);
                int index = newInstructions.FindLastIndex(i => i.opcode == OpCodes.Ldarg_0) - 1;

                System.Reflection.Emit.Label skipLabel = generator.DefineLabel();

                newInstructions[index].labels.Add(skipLabel);

                index = newInstructions.FindLastIndex(i => i.opcode == OpCodes.Brfalse_S) + 1;

                newInstructions.InsertRange(index, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PreventRpc), nameof(PreventRpc.IsBoombox))),
                    new CodeInstruction(OpCodes.Brtrue_S, skipLabel)
                });

                for (int z = 0; z < newInstructions.Count; z++) yield return newInstructions[z];
            }
        }

        [HarmonyPatch(typeof(BoomboxItem), nameof(BoomboxItem.PocketItem))]
        public class BoomboxPocketPatch
        {
            internal static bool Debounce = false;

            public static void Prefix()
            {
                Debounce = true;
            }
        }

        [HarmonyPatch(typeof(BoomboxItem), nameof(BoomboxItem.StartMusic))]
        public class BoomboxPatch
        {
            internal static bool ShowingGUI { get; set; } = false;
            internal static YoutubeBoomboxGUI ShownGUI { get; set; }
            internal static BoomboxItem CurrentBoombox { get; set; }

            public static bool Prefix(BoomboxItem __instance, bool startMusic, bool pitchDown)
            {

                if (BoomboxPocketPatch.Debounce)
                {
                    BoomboxPocketPatch.Debounce = false;

                    Networking.Broadcast($"{__instance.NetworkObjectId}|{pitchDown}", NetworkingSignatures.BOOMBOX_OFF_SIG);
                    //Singleton.Logger.LogInfo("Stopping boombox");

                    if (pitchDown)
                    {
                        __instance.StartCoroutine(__instance.musicPitchDown());
                    }
                    else
                    {
                        __instance.boomboxAudio.Stop();
                        __instance.boomboxAudio.PlayOneShot(__instance.stopAudios[UnityEngine.Random.Range(0, __instance.stopAudios.Length)]);
                    }

                    __instance.timesPlayedWithoutTurningOff = 0;

                    __instance.isBeingUsed = false;
                    __instance.isPlayingMusic = false;
                    ClientTracker.Reset(__instance);

                    CurrentBoombox = null;

                    return false;
                }

                __instance.isBeingUsed = startMusic;

                if (!startMusic)
                {
                    if (ShowingGUI)
                    {
                        //Singleton.Logger.LogInfo("Prevent dual open");
                        return false;
                    }

                    ClientTracker.Reset(__instance);

                    //Singleton.Logger.LogInfo("Opening boombox gui");

                    CurrentBoombox = __instance;

                    GameObject guiObj = new GameObject("YoutubeBoomboxInput");
                    guiObj.hideFlags = HideFlags.HideAndDontSave;
                    ShownGUI = guiObj.AddComponent<YoutubeBoomboxGUI>();

                    ShowingGUI = true;
                }
                else if (__instance.isPlayingMusic)
                {
                    Networking.Broadcast($"{__instance.NetworkObjectId}|{pitchDown}", NetworkingSignatures.BOOMBOX_OFF_SIG);
                    //Singleton.Logger.LogInfo("Stopping boombox");

                    if (pitchDown)
                    {
                        __instance.StartCoroutine(__instance.musicPitchDown());
                    }
                    else
                    {
                        __instance.boomboxAudio.Stop();
                        __instance.boomboxAudio.PlayOneShot(__instance.stopAudios[UnityEngine.Random.Range(0, __instance.stopAudios.Length)]);
                    }

                    __instance.timesPlayedWithoutTurningOff = 0;

                    __instance.isBeingUsed = false;
                    __instance.isPlayingMusic = false;
                    ClientTracker.Reset(__instance);

                    CurrentBoombox = null;
                }

                return false;
            }
        }
    }
}
