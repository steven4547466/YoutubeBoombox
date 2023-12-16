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
using GameNetcodeStuff;
using Unity.Netcode;
using System.Reflection.Emit;
using UnityEngine.Pool;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.ComponentModel;
using Newtonsoft.Json.Linq;
using BepInEx.Configuration;
using YoutubeDLSharp.Metadata;
using System.Security.Policy;
using Newtonsoft.Json;
using System.Reflection;
using System.Collections.Specialized;
using YoutubeBoombox.Providers;
using System.Diagnostics;

namespace YoutubeBoombox
{
    public class InfoCache : IProgress<string>
    {
        public class Info
        {
            public string id { get; set; }
            public float duration { get; set; }
        }

        public string Id { get; set; }

        public InfoCache(string id)
        {
            Id = id;

            PlaylistCache.Add(id, new List<string>());
        }

        public static Dictionary<string, float> DurationCache = new Dictionary<string, float>();
        public static Dictionary<string, List<string>> PlaylistCache = new Dictionary<string, List<string>>();

        public void Report(string value)
        {
            try
            {
                Info json = JsonConvert.DeserializeObject<Info>(value);

                if (!DurationCache.ContainsKey(json.id))
                {
                    DurationCache.Add(json.id, json.duration);
                }

                PlaylistCache[Id].Add(json.id);

            } catch { }
        }
    }

    [BepInPlugin("steven4547466.YoutubeBoombox", "Youtube Boombox", "1.5.0")]
    [BepInDependency("LC_API")]
    public class YoutubeBoombox : BaseUnityPlugin
    {
        private static Harmony Harmony { get; set; }

        internal static string DirectoryPath { get; private set; }

        internal static string DownloadsPath { get; private set; }

        internal static YoutubeBoombox Singleton { get; private set; }

        public static YoutubeDL YoutubeDL { get; private set; } = new YoutubeDL();

        #region Config
        internal static ConfigEntry<int> MaxCachedDownloads { get; private set; }

        internal static ConfigEntry<bool> DeleteDownloadsOnRestart { get; private set; }

        internal static ConfigEntry<float> MaxSongDuration { get; private set; }

        internal static ConfigEntry<bool> EnableDebugLogs { get; private set; }

        internal static ConfigEntry<Key> CustomBoomboxButton { get; private set; }
        #endregion

        internal static List<string> PathsThisSession { get; private set; } = new List<string>();

        internal static List<Provider> Providers { get; } = new List<Provider>();

        public static void LogInfo(object data)
        {
            Singleton.Logger.LogInfo(data);
        }

        public static void LogError(object data)
        {
            Singleton.Logger.LogError(data);
        }

        public static void DebugLog(object data, bool shouldLog = true)
        {
            if (shouldLog)
            {
                Singleton.Logger.LogInfo(data);
            }
        }

        async void Awake()
        {
            Singleton = this;

            MaxCachedDownloads = Config.Bind(new ConfigDefinition("General", "Max Cached Downloads"), 10, new ConfigDescription("The maximum number of downloaded songs that can be saved before deleting.", new ConfigNumberClamper(1, 100)));
            DeleteDownloadsOnRestart = Config.Bind("General", "Delete Downloads On Restart", true, "Whether or not to delete downloads when your game starts again.");
            MaxSongDuration = Config.Bind("General", "Max Song Duration", 600f, "Maximum song duration in seconds. Any video longer than this will not be downloaded.");

            EnableDebugLogs = Config.Bind("Debugging", "Enable Debug Logs", false, "Whether or not to enable debug logs.");

            CustomBoomboxButton = Config.Bind("Keybinds", "Open Menu Key", Key.B, "The button you need to press to open the youtube boombox gui.");

            string oldDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "Youtube-Boombox");

            if (Directory.Exists(oldDirectoryPath))
            {
                Directory.Delete(oldDirectoryPath, true);
            }

            DirectoryPath = Path.Combine(Paths.PluginPath, "steven4547466-YoutubeBoombox", "data");
            DownloadsPath = Path.Combine(DirectoryPath, "Downloads");

            if (!Directory.Exists(DirectoryPath)) Directory.CreateDirectory(DirectoryPath);
            if (!Directory.Exists(DownloadsPath)) Directory.CreateDirectory(DownloadsPath);

            if (DeleteDownloadsOnRestart.Value)
            {
                foreach (string file in Directory.GetFiles(DownloadsPath))
                {
                    File.Delete(file);
                }
            }

            if (!Directory.GetFiles(DirectoryPath).Any(file => file.Contains("yt-dl"))) await Utils.DownloadYtDlp(DirectoryPath);
            if (!Directory.GetFiles(DirectoryPath).Any(file => file.Contains("ffmpeg"))) await Utils.DownloadFFmpeg(DirectoryPath);

            YoutubeDL.YoutubeDLPath = Directory.GetFiles(DirectoryPath).First(file => file.Contains("yt-dl"));
            YoutubeDL.FFmpegPath = Directory.GetFiles(DirectoryPath).First(file => file.Contains("ffmpeg"));

            YoutubeDL.OutputFolder = DownloadsPath;

            YoutubeDL.OutputFileTemplate = "%(id)s.%(ext)s";

            Harmony = new Harmony($"steven4547466.YoutubeBoombox-{DateTime.Now.Ticks}");

            Harmony.PatchAll();

            SetupNetworking();

            LC_API.ClientAPI.CommandHandler.RegisterCommand("bbv", new List<string>() { "boomboxvolume" }, (string[] args) =>
            {
                if (args.Length > 0 && float.TryParse(args[0], out float volume))
                {
                    PlayerControllerB localController = StartOfRound.Instance.localPlayerController;
                    if (localController.currentlyHeldObjectServer is BoomboxItem boombox)
                    {
                        boombox.boomboxAudio.volume = volume / 100;
                    }
                    else
                    {
                        BoomboxItem closestBoombox = null;
                        float distanceSqr = float.MaxValue;

                        foreach (BoomboxItem boomboxItem in FindObjectsOfType<BoomboxItem>())
                        {
                            float dist = (boomboxItem.transform.position - localController.transform.position).sqrMagnitude;
                            if (dist < distanceSqr)
                            {
                                closestBoombox = boomboxItem;
                                distanceSqr = dist;
                            }
                        }

                        if (distanceSqr <= 255)
                        {
                            closestBoombox.boomboxAudio.volume = volume / 100;
                        }
                    }
                }
            });

            //LC_API.ClientAPI.CommandHandler.RegisterCommand("spawnbox", (string[] args) =>
            //{
            //    NetworkManager manager = FindObjectOfType<NetworkManager>();
            //    if (manager != null)
            //    {
            //        foreach (NetworkPrefab prefab in manager.NetworkConfig.Prefabs.Prefabs)
            //        {
            //            if (prefab.Prefab.TryGetComponent(out BoomboxItem boombox))
            //            {
            //                BoomboxItem spawnedBox = Instantiate(boombox, StartOfRound.Instance.localPlayerController.transform.position, default);
            //                spawnedBox.insertedBattery.charge = 0.2f;
            //                spawnedBox.GetComponent<NetworkObject>().Spawn();

            //                spawnedBox.SyncBatteryServerRpc(20);

            //                break;
            //            }
            //        }
            //    }
            //});

            var method = new StackTrace().GetFrame(0).GetMethod();
            var assembly = method.ReflectedType.Assembly;
            foreach (Type t in AccessTools.GetTypesFromAssembly(assembly))
            {
                if (t.IsSubclassOf(typeof(Provider)))
                {
                    Providers.Add(Activator.CreateInstance(t) as Provider);
                }
            }
        }

        private void SetupNetworking()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start))]
        class GameNetworkManagerPatch
        {
            public static void Postfix(GameNetworkManager __instance)
            {
                foreach (NetworkPrefab prefab in __instance.GetComponent<NetworkManager>().NetworkConfig.Prefabs.Prefabs)
                {
                    if (prefab.Prefab.GetComponent<BoomboxItem>() != null)
                    {
                        prefab.Prefab.AddComponent<BoomboxController>();

                        break;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.UseItemOnClient))]
        class PreventRpc
        {
            public static bool IsBoomboxAndGUIShowing(GrabbableObject obj)
            {
                return obj is BoomboxItem && obj.gameObject.TryGetComponent(out BoomboxController controller) && controller.IsGUIShowing();
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
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PreventRpc), nameof(PreventRpc.IsBoomboxAndGUIShowing))),
                    new CodeInstruction(OpCodes.Brtrue_S, skipLabel)
                });

                for (int z = 0; z < newInstructions.Count; z++) yield return newInstructions[z];
            }
        }

        [HarmonyPatch(typeof(BoomboxItem), nameof(BoomboxItem.StartMusic))]
        public class BoomboxPatch
        {
            internal static bool ShowingGUI { get; set; } = false;
            internal static YoutubeBoomboxGUI ShownGUI { get; set; }
            internal static BoomboxItem CurrentBoombox { get; set; }

            public static bool Prefix(BoomboxItem __instance)
            {
                if (__instance.TryGetComponent(out BoomboxController controller) && controller.IsGUIShowing())
                    return false;

                return true;
            }
        }
    }
}
