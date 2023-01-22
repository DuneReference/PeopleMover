﻿using Verse;
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
        public static int movespeedPathCost = 26;

        public static bool useExplicitPathingPathCost = false;
        public static int pathingPathCost = 20;

        public static bool showFlashingPathCost = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref movespeedPathCost, "additionalPathCost", movespeedPathCost);
            Scribe_Values.Look(ref useExplicitPathingPathCost, "useExplicitPathingPathCost", useExplicitPathingPathCost);
            Scribe_Values.Look(ref pathingPathCost, "pathingPathCost", pathingPathCost);
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
            listingStandard.Label("The speed of the mover. higher values is less walk speed.");
            listingStandard.Label($"{PeopleMoverSettings.movespeedPathCost} Path cost: ~{(38 - PeopleMoverSettings.movespeedPathCost) * 13} % walk speed");
            listingStandard.IntAdjuster(ref PeopleMoverSettings.movespeedPathCost, 1, 0);
            listingStandard.Label("Advanced:");
            listingStandard.CheckboxLabeled("Explicitly specify the pathCost increment of the mover.", ref PeopleMoverSettings.useExplicitPathingPathCost, "How much pawns prioritize/deprioritize walking on movers depending in direction.");
            listingStandard.Label($"{PeopleMoverSettings.pathingPathCost}");
            listingStandard.IntAdjuster(ref PeopleMoverSettings.pathingPathCost, 1, -50);
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