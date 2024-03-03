using RimWorld;
using System.Collections;
using System;
using System.Collections.Generic;
using Verse;
using static RimWorld.ColonistBar;

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

            Log.Message($"[DebugAction] Printing networksCache");

            for (int i = 0; i < networksCache.Count; i++)
            {
                var network = networksCache[i];
                // Log.Message($"[DebugAction] network {i}");

                for (int j = 0; j < network.Count; j++)
                {
                    // Log.Message($"[DebugAction] network {i}, cell {network[j].cell}, isHub? {network[j].isHub}");
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

        [DebugAction("PeopleMover", null, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void PrintMapCompCache()
        {
            var mapCompCache = VanillaPatches.mapsCompCache;

            Log.Message($"[DebugAction] Printing just mapCompCache");

            foreach (KeyValuePair<int, PeopleMoverMapComp> kvp in mapCompCache)
            {
                Log.Message($"Key: {kvp.Key}, Value: {kvp.Value}");
            }
        }
    }
}