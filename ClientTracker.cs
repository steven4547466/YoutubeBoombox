using BepInEx.Logging;
using BepInEx;
using LC_API.ServerAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace YoutubeBoombox
{
    public class ClientTracker
    {
        private static Dictionary<BoomboxItem, int> trackers = new Dictionary<BoomboxItem, int>();
        public static void AddReadyClient(BoomboxItem boombox, bool self = false)
        {
            if (!trackers.ContainsKey(boombox))
            {
                trackers.Add(boombox, 1);
            } 
            else
            {
                trackers[boombox]++;
            }

            if (self) Networking.Broadcast((int)boombox.NetworkObjectId, NetworkingSignatures.BOOMBOX_READY_CLIENT_SIG);

            YoutubeBoombox.Log($"READY CLIENT {trackers[boombox]}/{StartOfRound.Instance.connectedPlayersAmount + 1} | IS SELF?: {self}");

            if (trackers[boombox] >= StartOfRound.Instance.connectedPlayersAmount + 1)
            {
                boombox.isBeingUsed = true;
                boombox.isPlayingMusic = true;
                boombox.boomboxAudio.Play();
            }
        }

        public static void Reset(BoomboxItem boombox)
        {
            trackers[boombox] = 0;
        }

        public static void Clear()
        {
            trackers.Clear();
        }
    }
}
