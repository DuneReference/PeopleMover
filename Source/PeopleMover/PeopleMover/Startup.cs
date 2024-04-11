using Verse;
using UnityEngine;

namespace DuneRef_PeopleMover
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
        }
    }

    public class PeopleMoverSettings : ModSettings
    {
        // movespeed
        public static int movespeedPathCost = 26;
        public static int defaultMovespeedPathCost = 26;

        // wattage cost
        public static int wattagePerTerrain = 10;
        public static int defaultWattagePerTerrain = 10;

        public static int wattageHub = 10;
        public static int defaultWattageHub = 10;

        // pathcost
        public static bool useExplicitPathingPathCost = false;
        public static bool defaultUseExplicitPathingPathCost = false;

        public static int pathingPathCost = 20;
        public static int defaultPathingPathCost = 20;

        // debug
        public static bool showFlashingPathCost = false;

        public override void ExposeData()
        {
            // movespeed
            Scribe_Values.Look(ref movespeedPathCost, "movespeedPathCost", defaultMovespeedPathCost);

            // wattage cost
            Scribe_Values.Look(ref wattagePerTerrain, "wattagePerTerrain", defaultWattagePerTerrain);
            Scribe_Values.Look(ref wattageHub, "wattageHub", defaultWattageHub);
            
            // pathcost
            Scribe_Values.Look(ref useExplicitPathingPathCost, "useExplicitPathingPathCost", defaultUseExplicitPathingPathCost);
            Scribe_Values.Look(ref pathingPathCost, "pathingPathCost", defaultPathingPathCost);
            
            // debug
            Scribe_Values.Look(ref showFlashingPathCost, "showFlashingPathCost", showFlashingPathCost);
            base.ExposeData();
        }
    }

    public class PeopleMoverMod : Mod
    {
        public PeopleMoverSettings settings;

        public PeopleMoverMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<PeopleMoverSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            // movespeed
            listingStandard.Label($"The speed of the mover. higher values of path cost translates to less walk speed difference. (Mod Default: {PeopleMoverSettings.defaultMovespeedPathCost})");
            listingStandard.Label($"{PeopleMoverSettings.movespeedPathCost} Path cost: ~{(38 - PeopleMoverSettings.movespeedPathCost) * 13} % walk speed");
            PeopleMoverSettings.movespeedPathCost = (int)listingStandard.Slider(PeopleMoverSettings.movespeedPathCost, 0f, 100f);

            // wattage cost
            listingStandard.Label($"Wattage cost of terrain (Mod Default: {PeopleMoverSettings.defaultWattagePerTerrain})");
            listingStandard.Label($"{PeopleMoverSettings.wattagePerTerrain}");
            PeopleMoverSettings.wattagePerTerrain = (int)listingStandard.Slider(PeopleMoverSettings.wattagePerTerrain, 0f, 100f);

            listingStandard.Label($"Wattage cost of hub (Mod Default: {PeopleMoverSettings.defaultWattageHub})");
            listingStandard.Label($"{PeopleMoverSettings.wattageHub}");
            PeopleMoverSettings.wattageHub = (int)listingStandard.Slider(PeopleMoverSettings.wattageHub, 0f, 100f);

            // pathcost
            listingStandard.Label("Advanced:");
            listingStandard.CheckboxLabeled($"Explicitly specify the pathCost increment of the mover. (Mod Default: {PeopleMoverSettings.defaultPathingPathCost})", ref PeopleMoverSettings.useExplicitPathingPathCost, "How much pawns prioritize/deprioritize walking on movers depending in direction.");
            listingStandard.Label($"{PeopleMoverSettings.pathingPathCost}");
            PeopleMoverSettings.pathingPathCost = (int)listingStandard.Slider(PeopleMoverSettings.pathingPathCost, -50f, 100f);
            
            // debug
            listingStandard.Label("Dev:");
            listingStandard.CheckboxLabeled("Show flashing pathCost cells.", ref PeopleMoverSettings.showFlashingPathCost);
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "PeopleMover";
        }

        public override void WriteSettings()
        {
            base.WriteSettings();

            RecalculateWattages();
        }

        public void RecalculateWattages()
        {
            foreach (Map map in Find.Maps)
            {
                map.GetComponent<PeopleMoverMapComp>().ReapplyNetworksWattage();
            }
        }
    }
}