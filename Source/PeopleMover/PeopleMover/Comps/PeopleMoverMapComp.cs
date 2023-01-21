using Verse;
using System.Collections.Generic;

namespace DuneRef_PeopleMover
{
    public class NetworkItem
    {
        public IntVec3 cell;
        public int network;
        public bool isHub;
        public PeopleMoverPowerComp powerComp;

        public NetworkItem(IntVec3 cell, int network, bool isHub = false, PeopleMoverPowerComp powerComp = null)
        {
            this.cell = cell;
            this.isHub = isHub;
            this.network = network;
            this.powerComp = powerComp;
        }
    }

    public class PeopleMoverMapComp : MapComponent
    {
        // holds all the networks and all the cells in them
        public List<List<NetworkItem>> networksCache = new List<List<NetworkItem>>();

        // speeds up finding the hub in a network
        public Dictionary<int, NetworkItem> networksHubCache = new Dictionary<int, NetworkItem>();

        // speeds up finding a specific cell in the network
        public Dictionary<IntVec3, NetworkItem> cellHashMap = new Dictionary<IntVec3, NetworkItem>();

        // On network item add it searches the whole network and then does it again each time it adds something while searching
        // this stops us from checking the same cells every time it reloops through the network.
        public Dictionary<IntVec3, NetworkItem> refreshCellHashMap = new Dictionary<IntVec3, NetworkItem>();

        public PeopleMoverMapComp(Map map) : base(map)
        {

        }

        /*
         * Registers the mover specified and then checks the network to make sure
         * it has everything possibly connected now.
         */
        public void RegisterMover (IntVec3 newCell, bool isHub = false)
        {
            int networkWereUsing = RegisterSingleMover(newCell, isHub);

            /*
             * Each time something is added we recheck the whole network so we can
             * also add any rogue networks it may have connected.
             */

            // if something was added
            if (networkWereUsing != -1)
            {
                List<NetworkItem> network = networksCache[networkWereUsing];

                bool foundSomethingInNetworkThatHadntBeenChecked = true;

                while (foundSomethingInNetworkThatHadntBeenChecked)
                {
                    foundSomethingInNetworkThatHadntBeenChecked = false;

                    for (int i = 0; i < network.Count; i++)
                    {
                        NetworkItem networkItem = network[i];

                        if (!refreshCellHashMap.TryGetValue(networkItem.cell, out _))
                        {
                            foundSomethingInNetworkThatHadntBeenChecked = true;
                            refreshCellHashMap.Add(networkItem.cell, networkItem);
                            IEnumerable<IntVec3> myCellAdjacencies = GenAdj.CellsAdjacentCardinal(networkItem.cell, Rot4.North, new IntVec2(1, 1));

                            foreach (IntVec3 adjCell in myCellAdjacencies)
                            {
                                if (adjCell.GetTerrain(map).defName.Contains("DuneRef_PeopleMover_Terrain"))
                                {
                                    RegisterSingleMover(adjCell);
                                }
                            }

                            break;
                        }
                    }
                }
            }

            refreshCellHashMap.Clear();
        }

        /*
        * Registers a single mover, while doing so it updates the network we're using for the network refresh.
        */
        public int RegisterSingleMover (IntVec3 newCell, bool isHub = false)
        {
            IEnumerable<IntVec3> myCellAdjacencies = GenAdj.CellsAdjacentCardinal(newCell, Rot4.North, new IntVec2(1, 1));
            bool addedNewCell = false;
            int networkWereUsing = -1;

            if (getNetwork(newCell) == -1)
            {
                /*
                 * If it's a hub, add a new network
                 */

                if (isHub)
                {
                
                    List<NetworkItem> newNetwork = new List<NetworkItem>();
                    NetworkItem newNetworkItem = new NetworkItem(newCell, networksCache.Count, isHub, map.edificeGrid[map.cellIndices.CellToIndex(newCell)].GetComp<PeopleMoverPowerComp>());
                    newNetwork.Add(newNetworkItem);
                    networksHubCache[networksCache.Count] = newNetworkItem;

                    // used for adj iterations
                    networkWereUsing = networksCache.Count;

                    // used for long term lookups
                    networksCache.Add(newNetwork);
                    cellHashMap[newCell] = newNetworkItem;
                }
                else
                {
                    /*
                     * For each adjacent cell, if it's a mover or a hub, search all networks for it.
                     * If you find it, add our new cell to that network.
                     */
                    foreach (IntVec3 adjCell in myCellAdjacencies)
                    {
                        if (adjCell.GetTerrain(map).defName.Contains("DuneRef_PeopleMover_Terrain") || (map.edificeGrid[map.cellIndices.CellToIndex(adjCell)] != null && map.edificeGrid[map.cellIndices.CellToIndex(adjCell)].def.defName.Contains("DuneRef_PeopleMover_PowerHub")))
                        {

                            int networkCellIsIn = getNetwork(adjCell);

                            if (networkCellIsIn != -1)
                            {
                                NetworkItem newNetworkItem = new NetworkItem(newCell, networkCellIsIn);

                                // used for adj iterations
                                networkWereUsing = networkCellIsIn;

                                // used for long term lookups
                                networksCache[networkCellIsIn].Add(newNetworkItem);
                                cellHashMap[newCell] = newNetworkItem;

                                // update power on hub
                                PeopleMoverPowerComp powerComp = networksHubCache[networkCellIsIn].powerComp;
                                powerComp.UpdateDesiredPowerOutput(powerComp.desiredPowerOutput + 10f);

                                addedNewCell = true;
                            }
                        }

                        if (addedNewCell)
                        {
                            break;
                        }
                    }
                }
            }
            

            return networkWereUsing;
        }

        /*
         * On removal of terrain we just reregister the network instead of worrying about
         * breaking off parts of the network manually. If the hub is removed, just clear the network.
         */
        public void DeregisterMover (IntVec3 newCell, bool isHub = false)
        {
            if (cellHashMap.TryGetValue(newCell, out NetworkItem networkItem))
            {
                if (isHub)
                {
                    ClearNetwork(networksCache[networkItem.network]);
                } else
                {
                    IntVec3 hubCell = ClearNetwork(networksCache[networkItem.network]);
                    ReregisterNetwork(hubCell);
                }
            }
        }

        /*
         * register the initial hub to get the whole network back from the start.
         */
        public void ReregisterNetwork (IntVec3 hubCell)
        {
            if (hubCell != new IntVec3(-1, -1, -1))
            {
                RegisterMover(hubCell, true);
            }
        }

        /*
         * Clear hashmap and network list, and return the hubcell in case of reregistering the network.
         */
        public IntVec3 ClearNetwork(List<NetworkItem> network)
        {
            IntVec3 hubCell = new IntVec3(-1, -1, -1);

            foreach (NetworkItem networkItem in network)
            {
                if (networkItem.isHub)
                {
                    hubCell = networkItem.cell;
                    networksHubCache.Remove(networkItem.network);

                    // Reset power consumption for hub
                    PeopleMoverPowerComp powerComp = networkItem.powerComp;
                    powerComp.UpdateDesiredPowerOutput(10f);
                }

                cellHashMap.Remove(networkItem.cell);
            }

            network.Clear();

            return hubCell;
        }

        /*
         * Searches Hash map of cells to see if it's in a network.
         */
        public int getNetwork(IntVec3 cellToCheck)
        {
            int networkCellIsIn = -1;
            NetworkItem networkItem;

            if (cellHashMap.TryGetValue(cellToCheck, out networkItem)) 
            {
                networkCellIsIn = networkItem.network;
            }

            return networkCellIsIn;
        }
    
        /*
         * Finds the hub of this cells network and checks its power status
         */
        public bool isCellsNetworkPowered(IntVec3 CellToCheck)
        {
            bool powered = false;

            int networkIndex = getNetwork(CellToCheck);

            if (networkIndex != -1)
            {
                NetworkItem networkItem = networksHubCache[networkIndex];

                if (networkItem.powerComp != null)
                {
                    powered = networkItem.powerComp.PowerOn;
                }
            }

            return powered;
        }
    }
}