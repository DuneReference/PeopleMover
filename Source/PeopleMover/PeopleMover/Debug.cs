using RimWorld;
using System.Collections.Generic;
using Verse;

namespace DuneRef_PeopleMover
{
    [StaticConstructorOnStartup]
    public static class Debug
    {
        static Debug()
        {
        }

        [DebugAction("PeopleMover", null, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void PrintNetworks()
        {
            var networksCache = Find.CurrentMap.GetComponent<PeopleMoverMapComp>().networksCache;

            for (int i = 0; i < networksCache.Count; i++)
            {
                var network = networksCache[i];

                for (int j = 0; j < network.Count; j++)
                {
                    Find.CurrentMap.debugDrawer.FlashCell(network[j].cell, 50, $"Net {i}", 100);
                }
            }
        }

        [DebugAction("PeopleMover", null, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void PrintNetworksHubCache()
        {
            var networksHubCache = Find.CurrentMap.GetComponent<PeopleMoverMapComp>().networksHubCache;

            Log.Message($"[DebugAction] Printing just networksHubCache");

            foreach (KeyValuePair<int, NetworkItem> entry in networksHubCache)
            {
                Log.Message($"[DebugAction] network {entry.Key}, cell {entry.Value.cell}, isHub? {entry.Value.isHub}");
            }
        }
    }
}