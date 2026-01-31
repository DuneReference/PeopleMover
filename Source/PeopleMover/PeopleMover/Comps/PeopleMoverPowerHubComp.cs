using Verse;

namespace DuneRef_PeopleMover
{
    public class PeopleMoverPowerHubComp : ThingComp
    {
        public PeopleMoverMapComp mapComp;
        public IntVec3 cell = new IntVec3(-1, -1, -1);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            /*
             * Adds hubs at start of game which search out for their whole network
             */

            mapComp = parent.Map.GetComponent<PeopleMoverMapComp>();
            cell = parent.Position;

            mapComp.RegisterMover(cell, true);
        }

        public override void PostDeSpawn(Map map, DestroyMode destroyMode)
        {
            base.PostDeSpawn(map, destroyMode);

            /*
             * When hubs are removed, their network is destroyed
             */

            mapComp.DeregisterMover(cell, true);
        }
    }
}