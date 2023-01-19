using RimWorld;
using Verse;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse.Noise;

namespace DuneRef_PeopleMover
{
    public class NetworkItem
    {
        public IntVec3 cell = new IntVec3();
        public bool isHub = false;

        public NetworkItem(IntVec3 cell, bool isHub = false)
        {
            this.cell = cell;
            this.isHub = isHub;
            Log.Message($"[NetworkItem.ctor] Item created. isHub? {isHub}");
        }
    }

    public class PeopleMoverMapComp : MapComponent
    {
        public List<List<NetworkItem>> NetworksCache = new List<List<NetworkItem>>();

        public PeopleMoverMapComp(Map map) : base(map)
        {
            Log.Message($"[PeopleMoverMapComp] PeopleMoverMapComp");
        }
        public override void FinalizeInit()
        {
            base.FinalizeInit();
/*            Log.Message($"[FinalizeInit] FinalizeInit");

            foreach (IntVec3 cell in map.AllCells)
            {
                if (cell.GetTerrain(map).defName.Contains("DuneRef_PeopleMover_Terrain"))
                {
                    if (getNetwork(cell) == -1)
                    {
                        Log.Message($"[FinalizeInit] Let's start the register");
                        RegisterConveyor(cell);
                    }
                }
            }*/
        }

        public void RegisterConveyor (IntVec3 newCell, bool isHub = false)
        {
            IEnumerable<IntVec3> myCellAdjacencies = GenAdj.CellsAdjacentCardinal(newCell, Rot4.North, new IntVec2(1, 1));

            bool addedNewCell = false;
            IntVec3 adjCellThatConnectedUs = new IntVec3(0, 0, 0);

            /*
             * If it's a hub, add a new network
             */

            if (isHub)
            {
                List <NetworkItem> newNetwork = new List<NetworkItem>();
                newNetwork.Add(new NetworkItem(newCell, isHub));

                NetworksCache.Add(newNetwork);
                Log.Message($"[RegisterConveyor] Hub at {newCell.x}, {newCell.z} was registered into network {NetworksCache.Count}");

                addedNewCell = true;
                adjCellThatConnectedUs = newCell;
            } else
            {
                /*
                 * 
                 * For each adjacent cell, if it's a conveyor or a hub, search all networks for it.
                 * If you find it, add our new cell to that network.
                 * 
                 */

                foreach (IntVec3 adjCell in myCellAdjacencies)
                {
                    Log.Message($"[RegisterConveyor] Lets test adjacency at {adjCell.x}, {adjCell.z}...");
                    Building building = map.edificeGrid[map.cellIndices.CellToIndex(adjCell)];

                    if (adjCell.GetTerrain(map).defName.Contains("DuneRef_PeopleMover_Terrain") || (building != null && building.def.defName.Contains("DuneRef_PeopleMover_PowerHub")))
                    {

                        int networkCellIsIn = getNetwork(adjCell);

                        if (networkCellIsIn != -1)
                        {
                            NetworksCache[networkCellIsIn].Add(new NetworkItem(newCell));
                            Log.Message($"[RegisterConveyor] Conveyor at {newCell.x}, {newCell.z} was registered into network {networkCellIsIn}");
                            addedNewCell = true;
                            adjCellThatConnectedUs = adjCell;
                        }
                    }
                    else
                    {
                        Log.Message($"[RegisterConveyor] not terrain or hub...");
                    }

                    if (addedNewCell)
                    {
                        break;
                    }
                }
            }

            if (!addedNewCell)
            {
                Log.Message($"[RegisterConveyor] Conveyor at {newCell.x}, {newCell.z} could not be registered");
            }

            /*
             * 
             * If we added our new cell, let's see if any of it's adjacencies have unnetworked conveyors we can add.
             * for each adj cell, if it's not the one that connected us before, and it's a conveyor, search the networks for it.
             * If this adjacent cell, which is a conveyor, isn't in any networks, then we need to add it.
             * But we also want to do this recursively so we'll call this function we're in again.
             * Note: This does mean that I'm searching all the networks twice for each add after the original one.
             * 
             */

            if (addedNewCell)
            {
                foreach (IntVec3 adjCell in myCellAdjacencies)
                {
                    Log.Message($"[RegisterConveyor] Lets test recursive adjacency at {adjCell.x}, {adjCell.z}...");
                    if (adjCell != adjCellThatConnectedUs)
                    {
                        if (adjCell.GetTerrain(map).defName.Contains("DuneRef_PeopleMover_Terrain"))
                        {
                            if (getNetwork(adjCell) == -1)
                            {
                                Log.Message("$[RegisterConveyor] Doing a little bit of recursion");
                                RegisterConveyor(adjCell);
                            }
                        } else
                        {
                            Log.Message($"[RegisterConveyor] not terrain...");
                        }
                    }
                }
            }
        }

        public void DeregisterConveyor ()
        {

        }

        public int getNetwork(IntVec3 cellToCheck)
        {
            Log.Message($"[getNetwork]: {cellToCheck}");
            int networkCellIsIn = -1;

            for (int i = 0; i < NetworksCache.Count; i++)
            {
                List <NetworkItem> network = NetworksCache[i];
                Log.Message($"[getNetwork]: Network {i}...");

                foreach (NetworkItem networkItem in network)
                {
                    Log.Message($"[getNetwork]: NetworkItem {networkItem.cell.x}, {networkItem.cell.z}...");
                    Log.Message($"[getNetwork]: vs cellToCheck {cellToCheck.x}, {cellToCheck.z}...");
                    if (networkItem.cell == cellToCheck)
                    {
                        Log.Message($"[getNetwork]: Found in network {i}!");
                        networkCellIsIn = i;
                        break;
                    }
                }

                if (networkCellIsIn != -1)
                {
                    break;
                }
            }

            if (networkCellIsIn == -1)
            {
                Log.Message($"[getNetwork]: Not Found.");
            }

            return networkCellIsIn;
        }
    }
}