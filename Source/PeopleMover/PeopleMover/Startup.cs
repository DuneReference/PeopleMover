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

        public override void ExposeData()
        {
            // movespeed
            Scribe_Values.Look(ref movespeedPathCost, "movespeedPathCost", defaultMovespeedPathCost);

            // wattage cost
            Scribe_Values.Look(ref wattagePerTerrain, "wattagePerTerrain", defaultWattagePerTerrain);
            Scribe_Values.Look(ref wattageHub, "wattageHub", defaultWattageHub);
            
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
            
            if (Current.Game?.Maps == null) return;

            foreach (Map map in Find.Maps)
            {
                if (map != null)
                {
                    PeopleMoverMapComp peopleMoverMapComp = map.GetComponent<PeopleMoverMapComp>();

                    if (peopleMoverMapComp != null)
                    {
                        map.GetComponent<PeopleMoverMapComp>().ReapplyNetworksWattage();
                    }
                }
            }
        }
    }
}