using RimWorld;
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
                    // Log.Message($"[DebugAction] network {i}, cell {network[j].cell}");
                    Find.CurrentMap.debugDrawer.FlashCell(network[j].cell, 50, $"Net {i}", 100);
                }
            }
        }
    }
}