using Verse;
using HarmonyLib;

namespace DuneRef_PeopleMover
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        public static Harmony Harm;

        static HarmonyPatches()
        {
            Harm = new Harmony("rimworld.mod.duneref.peoplemover");

            VanillaPatches.Patches();
        }
    }
}