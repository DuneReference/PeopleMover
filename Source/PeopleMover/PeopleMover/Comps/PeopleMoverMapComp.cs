using RimWorld;
using Verse;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse.Noise;
using System.Collections;

namespace DuneRef_PeopleMover
{
    public class NetworkItem
    {
        public IntVec3 cell;
        public int network;
        public bool isHub;
        public CompPowerTrader powerComp;

        public NetworkItem(IntVec3 cell, int network, bool isHub = false, CompPowerTrader powerComp = null)
        {
            this.cell = cell;
            this.isHub = isHub;
            this.network = network;
            this.powerComp = powerComp;
        }
    }

    public class PeopleMoverMapComp : MapComponent
    {
        public List<List<NetworkItem>> NetworksCache = new List<List<NetworkItem>>();
        public Dictionary<IntVec3, NetworkItem> CellHashMap = new Dictionary<IntVec3, NetworkItem>();

        // Used for network checking on add
        public Dictionary<IntVec3, NetworkItem> RefreshCellHashMap = new Dictionary<IntVec3, NetworkItem>();

        public PeopleMoverMapComp(Map map) : base(map)
        {

        }

        /*
         * Registers the conveyor specified and then checks the network to make sure
         * it has everything possibly connected now.
         */
        public void RegisterConveyor (IntVec3 newCell, bool isHub = false)
        {
            int networkWereUsing = RegisterSingleConveyor(newCell, isHub);

            /*
             * Each time something is added we recheck the whole network so we can
             * also add any rogue networks it may have connected.
             */

            // if something was added
            if (networkWereUsing != -1)
            {
                // as long as 1 of the 4 adj didn't return a -1
                List<NetworkItem> network = NetworksCache[networkWereUsing];

                bool foundSomethingInNetworkThatHadntBeenChecked = true;

                while (foundSomethingInNetworkThatHadntBeenChecked)
                {
                    foundSomethingInNetworkThatHadntBeenChecked = false;

                    for (int i = 0; i < network.Count; i++)
                    {
                        NetworkItem networkItem = network[i];

                        if (!RefreshCellHashMap.TryGetValue(networkItem.cell, out _))
                        {
                            foundSomethingInNetworkThatHadntBeenChecked = true;
                            RefreshCellHashMap.Add(networkItem.cell, networkItem);
                            IEnumerable<IntVec3> myCellAdjacencies = GenAdj.CellsAdjacentCardinal(networkItem.cell, Rot4.North, new IntVec2(1, 1));

                            foreach (IntVec3 adjCell in myCellAdjacencies)
                            {
                                if (adjCell.GetTerrain(map).defName.Contains("DuneRef_PeopleMover_Terrain"))
                                {
                                    RegisterSingleConveyor(adjCell);
                                }
                            }

                            break;
                        }
                    }
                }
            }

            RefreshCellHashMap.Clear();
        }

        /*
        * Registers a single conveyor, while doing so it updates the network we're using for the network refresh.
        */
        public int RegisterSingleConveyor (IntVec3 newCell, bool isHub = false)
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
                    NetworkItem newNetworkItem = new NetworkItem(newCell, NetworksCache.Count, isHub, map.edificeGrid[map.cellIndices.CellToIndex(newCell)].GetComp<CompPowerTrader>());
                    newNetwork.Add(newNetworkItem);

                    // used for adj iterations
                    networkWereUsing = NetworksCache.Count;

                    // used for long term lookups
                    NetworksCache.Add(newNetwork);
                    CellHashMap[newCell] = newNetworkItem;
                }
                else
                {
                    /*
                     * For each adjacent cell, if it's a conveyor or a hub, search all networks for it.
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
                                NetworksCache[networkCellIsIn].Add(newNetworkItem);
                                CellHashMap[newCell] = newNetworkItem;

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
         * breaking off parts of the network manually
         */
        public void DeregisterConveyor (IntVec3 newCell, bool isHub = false)
        {
            bool foundCell = false;

            for (int i = 0; i < NetworksCache.Count; i++)
            {
                List<NetworkItem> network = NetworksCache[i];

                foreach (NetworkItem networkItem in network)
                {
                    if (networkItem.cell == newCell)
                    {
                        ReregisterNetwork(network);

                        foundCell = true;
                        break;
                    }
                }

                if (foundCell)
                {
                    break;
                }
            }
        }

        /*
         * Clear hashmap and network list, then register the initial hub to get the whole network back from the start.
         */
        public void ReregisterNetwork (List<NetworkItem> network)
        {
            IntVec3 hubCell = new IntVec3(-1, -1, -1);

            foreach (NetworkItem networkItem in network)
            {
                if (networkItem.isHub)
                {
                    hubCell = networkItem.cell;
                }

                CellHashMap.Remove(networkItem.cell);
            }

            network.Clear();

            if (hubCell != new IntVec3(-1, -1, -1))
            {
                RegisterConveyor(hubCell, true);
            }
        }

        /*
         * Searches Hash map of cells to see if it's in a network.
         */
        public int getNetwork(IntVec3 cellToCheck)
        {
            int networkCellIsIn = -1;
            NetworkItem networkItem;

            if (CellHashMap.TryGetValue(cellToCheck, out networkItem)) 
            {
                networkCellIsIn = networkItem.network;
            }

            return networkCellIsIn;
        }
    
        public bool isCellsNetworkPowered(IntVec3 CellToCheck)
        {
            bool powered = false;

            int networkIndex = getNetwork(CellToCheck);

            if (networkIndex != -1)
            {
                for (int i = 0; i < NetworksCache[networkIndex].Count; i++)
                {
                    NetworkItem networkItem = NetworksCache[networkIndex][i];

                    if (networkItem.isHub)
                    {
                        if (networkItem.powerComp != null)
                        {
                            powered = networkItem.powerComp.PowerOn;
                        }
                        
                        break;
                    }
                }
            }

            return powered;
        }
    }
}