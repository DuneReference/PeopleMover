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

        // pathcost
        public static bool useExplicitPathingPathCost = false;
        public static bool defaultUseExplicitPathingPathCost = false;

        public static int pathingPathCost = 20;
        public static int defaultPathingPathCost = 20;

        // debug
        public static bool showFlashingPathCost = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref movespeedPathCost, "additionalPathCost", defaultMovespeedPathCost);
            Scribe_Values.Look(ref useExplicitPathingPathCost, "useExplicitPathingPathCost", defaultUseExplicitPathingPathCost);
            Scribe_Values.Look(ref pathingPathCost, "pathingPathCost", defaultPathingPathCost);
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
            listingStandard.Label($"The speed of the mover. higher values of path cost translates to less walk speed difference. (Mod Default: {PeopleMoverSettings.defaultMovespeedPathCost})");
            listingStandard.Label($"{PeopleMoverSettings.movespeedPathCost} Path cost: ~{(38 - PeopleMoverSettings.movespeedPathCost) * 13} % walk speed");
            PeopleMoverSettings.movespeedPathCost = (int)listingStandard.Slider(PeopleMoverSettings.movespeedPathCost, 0f, 100f);
            listingStandard.Label("Advanced:");
            listingStandard.CheckboxLabeled($"Explicitly specify the pathCost increment of the mover. (Mod Default: {PeopleMoverSettings.defaultPathingPathCost})", ref PeopleMoverSettings.useExplicitPathingPathCost, "How much pawns prioritize/deprioritize walking on movers depending in direction.");
            listingStandard.Label($"{PeopleMoverSettings.pathingPathCost}");
            PeopleMoverSettings.pathingPathCost = (int)listingStandard.Slider(PeopleMoverSettings.pathingPathCost, -50f, 100f);
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
        }
    }
}