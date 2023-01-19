using Verse;
using System;
using RimWorld;
using System.Collections;

namespace DuneRef_PeopleMover
{
    public class PeopleMoverPowerHubComp : ThingComp
    {
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            PeopleMoverMapComp mapComp = parent.Map.GetComponent<PeopleMoverMapComp>();

            Log.Message($"[PostSpawnSetup] Adding hub from comp located at {parent.Position.x},{parent.Position.z}");
            mapComp.RegisterConveyor(parent.Position, true);
        }
    }
}