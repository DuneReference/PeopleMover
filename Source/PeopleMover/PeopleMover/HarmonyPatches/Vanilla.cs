using System;

using Verse;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Verse.AI;
using Verse.Profile;

namespace DuneRef_PeopleMover
{
    public static class VanillaPatches
    {
        public static readonly Type patchType = typeof(VanillaPatches);
        public static Harmony Harm = HarmonyPatches.Harm;

        // used to hold cache of map comps so FindPath is faster.
        public static Dictionary<int, PeopleMoverMapComp> mapsCompCache = new Dictionary<int, PeopleMoverMapComp>();

        public static void Patches()
        {
            // Patch CalculatedCostAt to allow PeopleMover's pathcost to make tiles pathcost lower than terrain's pathcost.
            // this is used as an entry level pathCost calculator which the game caches. updated on game load and terrain change.
            Harm.Patch(AccessTools.Method(typeof(PathGrid), "CalculatedCostAt"), transpiler: new HarmonyMethod(patchType, nameof(ChangePathCostInRepeaterSection)));
            Harm.Patch(AccessTools.Method(typeof(PathGrid), "CalculatedCostAt"), transpiler: new HarmonyMethod(patchType, nameof(ChangePathCostInSnowSandSection)));

            // Patch CostToMoveIntoCell for changing speed of pawn on the mover
            Harm.Patch(
                AccessTools.Method(
                    typeof(Pawn_PathFollower), 
                    "CostToMoveIntoCell", 
                    new Type[] { typeof(Pawn), typeof(IntVec3) }
                ), 
                transpiler: new HarmonyMethod(patchType, nameof(ChangePathCostInEdificeSection)));

            /* Patch SetTerrain to add/remove movers to network */
            Harm.Patch(AccessTools.Method(typeof(TerrainGrid), "SetTerrain"), postfix: new HarmonyMethod(patchType, nameof(AddNewTerrainToNetworkPostfix)));
            Harm.Patch(AccessTools.Method(typeof(TerrainGrid), "RemoveTopLayer"), prefix: new HarmonyMethod(patchType, nameof(RemoveTerrainFromNetworkPrefix)));
            Harm.Patch(AccessTools.Method(typeof(TerrainGrid), "RemoveTopLayer"), postfix: new HarmonyMethod(patchType, nameof(RemoveTerrainFromNetworkPostfix)));

            /* Patch in my new PeopleMoverPowerComp into ConnectToPower */
            Harm.Patch(AccessTools.Method(typeof(ThingDef), "get_ConnectToPower"), postfix: new HarmonyMethod(patchType, nameof(AddPowerCompToConnectionListPostfix)));

            /* Patch to clear mapcache on going to main menu or loading */
            Harm.Patch(AccessTools.Method(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld)), postfix: new HarmonyMethod(patchType, nameof(ClearAllMapsAndWorldPostfix)));
        }

        /* Utility functions */
        public static float GetPathCost(float defaultCost)
        {
            float returningPathCost = defaultCost;

            float costIncrement = 20f;

            return returningPathCost - costIncrement;
        }

        public static bool IsMapIndexApartOfPeopleMoverPowerHubNetwork(IntVec3 cell, Map map)
        {
            //if (map == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::IsMapIndexApartOfPeopleMoverPowerHubNetwork : map was null"); return false; }

            if (mapsCompCache.TryGetValue(map.uniqueID, out PeopleMoverMapComp mapComp))
            {
                return mapComp.IsCellsNetworkPowered(cell);
            } else
            {
                mapsCompCache[map.uniqueID] = map.GetComponent<PeopleMoverMapComp>();
                return mapsCompCache[map.uniqueID].IsCellsNetworkPowered(cell);
            }
        }

        /* CalculatedCostAt */
        public static IEnumerable<CodeInstruction> ChangePathCostInRepeaterSection(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            /*
             * The code in the disassembly looks like this: 
             *  // I've replaced "num" with "currentPathCost" here for clarity of what it is representing.
             *  if (!PathGrid.IsPathCostIgnoreRepeater(thing.def) || !prevCell.IsValid || !this.ContainsPathCostIgnoreRepeater(prevCell))
             *  {
             *      int pathCost = thing.def.pathCost; // the thing we're on top of
             *      if (pathCost > currentPathCost)
             *      {
             *          currentPathCost = pathCost;
             *      }
             *  }
             *  
             *  We are removing the if(pathCost > currentPathCost)... if statement and replacing it in its entirety.
             *  In our replacement function we maintain this logic--if the building pathCost is higher than the terrain's use the building's.
             *  However, we add in that if it's the PeopleMover and the power is on then if the pathCost of the building pathCost is LOWER than the
             *  terrain's then we use that lower value. Bypassing the fact vanilla can't have a lower pathcost than the terrain's pathcost.
            */
            try
            {
                CodeMatch[] desiredInstructions = new CodeMatch[]{
                    // IL_011d: ble.s IL_0122
                    new CodeMatch(i => i.opcode == OpCodes.Ble_S),
                    // IL_011f: ldloc.s 9
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_S),
                    // IL_0121: stloc.0
                    new CodeMatch(i => i.opcode == OpCodes.Stloc_0),
                    // IL_0122: ldloc.s 6
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_S),
                    // IL_0124: isinst RimWorld.Building_Door
                    new CodeMatch(i => i.opcode == OpCodes.Isinst),
                    // IL_0129: stloc.s 7
                    new CodeMatch(i => i.opcode == OpCodes.Stloc_S),
                };

                return new CodeMatcher(instructions, generator)
                    .Start()
                    .MatchStartForward(desiredInstructions)
                    .ThrowIfInvalid("Couldn't find the desired instructions")
                    .RemoveInstructions(3)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_S, 6))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(VanillaPatches.ChangePathCostInRepeaterSectionFn))))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Stloc_0))
                    .InstructionEnumeration();
            }
            catch (Exception ex)
            {
                Log.Error($"[DuneRef_PeopleMover] : {ex}");
                return instructions;
            }
        }

        public static int ChangePathCostInRepeaterSectionFn(int buildingPathCost, int terrainPathCost, Thing building)
        {
            int returningPathCost = buildingPathCost > terrainPathCost ? buildingPathCost : terrainPathCost;

            // if (building == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::ChangePathCostInRepeaterSectionFn : building was null"); return returningPathCost; }
            Def def = building.def;
            // if (def == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::ChangePathCostInRepeaterSectionFn : def was null"); return returningPathCost; }

            if ((def.defName == "DuneRef_PeopleMover" || def.defName == "DuneRef_PeopleMover_PowerHub") && building.TryGetComp<PeopleMoverPowerComp>().PowerOn)
            {
                if (buildingPathCost < terrainPathCost)
                {
                    returningPathCost = buildingPathCost;
                }
            }

            return returningPathCost;
        }

        public static IEnumerable<CodeInstruction> ChangePathCostInSnowSandSection(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            /* 
             * The code in the disassembly looks like this: 
             * // I've replaced "num" with "currentPathCost", "num2" with "snowPathCost" and "num3" with "sandPathCost" here for clarity of what it is representing.
             *   int currentPathCost = terrainDef.pathCost;
	         *   int snowPathCost = WeatherBuildupUtility.MovementTicksAddOn(this.map.snowGrid.GetCategory(c));
	         *   if (snowPathCost > currentPathCost)
	         *   {
		     *       currentPathCost = snowPathCost;
	         *   }
	         *   if (ModsConfig.OdysseyActive)
	         *   {
		     *       int sandPathCost = WeatherBuildupUtility.MovementTicksAddOn(this.map.sandGrid.GetCategory(c));
		     *       if (sandPathCost > currentPathCost)
		     *       {
			 *           currentPathCost = sandPathCost;
		     *       }
	         *   }
             *   
             *   We're replacing the assignments in both the snow and sand parts with the same assignments its already doing normally, 
             *   but if it's our building/terrain and its powered we want to ignore them.
             */
            try
            {
                CodeMatch[] desiredInstructions = new CodeMatch[]{
                    // IL_0182: callvirt instance valuetype Verse.WeatherBuildupCategory Verse.SnowGrid::GetCategory(valuetype Verse.IntVec3)
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt),
                    // IL_0187: call int32 Verse.WeatherBuildupUtility::MovementTicksAddOn(valuetype Verse.WeatherBuildupCategory)
                    new CodeMatch(i => i.opcode == OpCodes.Call),
                    // IL_018c: stloc.s 4
                    new CodeMatch(i => i.opcode == OpCodes.Stloc_S),
                    // IL_018e: ldloc.s 4
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_S),
                    // IL_0190: ldloc.0
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_0),
                    // IL_0191: ble.s IL_0196
                    new CodeMatch(i => i.opcode == OpCodes.Ble_S),
                    // IL_0193: ldloc.s 4
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_S)
                };

                return new CodeMatcher(instructions, generator)
                    .Start()
                    .MatchEndForward(desiredInstructions)
                    .ThrowIfInvalid("Couldn't find the desired instructions")
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_0))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_S, 6))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_S, 2))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldarg_0))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldarg_1))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(VanillaPatches.ChangePathCostInSnowSandSectionFn))))
                    // now that we've handled the snow portion, let's head to the sand portion.
                    .Advance(15)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_0))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_S, 6))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_S, 2))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldarg_0))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldarg_1))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(VanillaPatches.ChangePathCostInSnowSandSectionFn))))
                    .InstructionEnumeration();
            }
            catch (Exception ex)
            {
                Log.Error($"[DuneRef_PeopleMover] : {ex}");
                return instructions;
            }
        }

        public static int ChangePathCostInSnowSandSectionFn(int snowSandPathCost, int runningPathCost, Thing building, TerrainDef terrain, PathGrid pathGrid, IntVec3 nextCell)
        {
            int returningPathCost = snowSandPathCost;

            if (building != null)
            {
                Def def = building.def;
                //if (def == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::ChangePathCostInSnowSectionFn : def was null"); return returningPathCost; }

                if (def.defName == "DuneRef_PeopleMover" || def.defName == "DuneRef_PeopleMover_PowerHub")
                {
                    if (building.TryGetComp<PeopleMoverPowerComp>().PowerOn)
                    {
                        returningPathCost = runningPathCost;
                    }
                }
            } else
            {
                //if (terrain == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::ChangePathCostInSnowSectionFn : terrain was null"); return returningPathCost; }

                if (terrain.defName == "DuneRef_PeopleMover_Terrain")
                {
                    //if (pathGrid == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::ChangePathCostInSnowSectionFn : pathGrid was null"); return returningPathCost; }
                    Map map = pathGrid.map;
                    //if (map == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::ChangePathCostInSnowSectionFn : map was null"); return returningPathCost; }

                    if (IsMapIndexApartOfPeopleMoverPowerHubNetwork(nextCell, map))
                    {
                        returningPathCost = runningPathCost;
                    }
                }
            }

            return returningPathCost;
        }

        /* CostToMoveIntoCell */
        public static IEnumerable<CodeInstruction> ChangePathCostInEdificeSection(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            /* 
             * The code in the disassembly looks like this: 
             * // I've replaced "num" with "currentPathCost" and "edifice" with "building" here for clarity of what it is representing.
             *   	if (building != null)
	         *      {
		     *          currentPathCost += (float)building.PathWalkCostFor(pawn);
	         *      }
             *   
             *   We're replacing this whole if statement with our own logic which allows us to modify the pathCost based on the directionality
             *   of the pawn in relation to the direction the movers are going.
             */
            try
            {
                CodeMatch[] desiredInstructions = new CodeMatch[]{
                    // IL_0073: ldloc.2
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_2),
                    // IL_0074: brfalse.s IL_0081
                    new CodeMatch(i => i.opcode == OpCodes.Brfalse_S),
                    // IL_0076: ldloc.0
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_0),
                    // IL_0077: ldloc.2
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_2),
                    // IL_0078: ldarg.0
                    new CodeMatch(i => i.opcode == OpCodes.Ldarg_0),
                    // IL_0079: callvirt instance uint16 Verse.Building::PathWalkCostFor(class Verse.Pawn)
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt),
                    // IL_007e: conv.r4
                    new CodeMatch(i => i.opcode == OpCodes.Conv_R4),
                    // IL_007f: add
                    new CodeMatch(i => i.opcode == OpCodes.Add),
                    // IL_0080: stloc.0
                    new CodeMatch(i => i.opcode == OpCodes.Stloc_0)
                };

                return new CodeMatcher(instructions, generator)
                    .Start()
                    .MatchStartForward(desiredInstructions)
                    .ThrowIfInvalid("Couldn't find the desired instructions")
                    .RemoveInstructions(8)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_0))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_2))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldarg_0))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldarg_1))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(VanillaPatches.ChangePathCostInEdificeSectionFn))))
                    .InstructionEnumeration();
            }
            catch (Exception ex)
            {
                Log.Error($"[DuneRef_PeopleMover] : {ex}");
                return instructions;
            }
        }

        public static float ChangePathCostInEdificeSectionFn(float currentPathCost, Building building, Pawn pawn, IntVec3 newCell)
        {
            float returningPathCost = currentPathCost;

            //if (pawn == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::ChangePathCostInEdificeSectionFn : pawn was null"); return returningPathCost; }

            if (building != null) 
            {
                Def def = building.def;
                //if (def == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::ChangePathCostInEdificeSectionFn : def was null"); return returningPathCost; }

                if ((def.defName == "DuneRef_PeopleMover" || def.defName == "DuneRef_PeopleMover_PowerHub") && building.GetComp<PeopleMoverPowerComp>().PowerOn)
                {
                    float pathCost = PeopleMoverSettings.movespeedPathCost;

                    returningPathCost = GetPathCost(pathCost);
                }
                else
                {
                    returningPathCost += building.PathWalkCostFor(pawn);
                }
            } else
            {
                Map map = pawn.Map;
                //if (map == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::ChangePathCostInEdificeSectionFn : map was null"); return returningPathCost; }
                TerrainGrid terrainGrid = map.terrainGrid;
                //if (terrainGrid == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::ChangePathCostInEdificeSectionFn : terrainGrid was null"); return returningPathCost; }

                TerrainDef terrain = terrainGrid.TerrainAt(newCell);

                if (terrain.defName.Contains("DuneRef_PeopleMover_Terrain"))
                {
                    if (IsMapIndexApartOfPeopleMoverPowerHubNetwork(newCell, map))
                    {
                        float pathCost = PeopleMoverSettings.movespeedPathCost;

                        returningPathCost = GetPathCost(pathCost);
                    }
                }
            }

            return returningPathCost;
        }

        /* Terrain Patches */
        public static void AddNewTerrainToNetworkPostfix(IntVec3 c, TerrainDef newTerr, TerrainGrid __instance)
        {
            //if (newTerr == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::AddNewTerrainToNetworkPostfix : newTerr was null"); return; }

            if (newTerr.defName.Contains("DuneRef_PeopleMover_Terrain"))
            {
                //if (__instance == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::AddNewTerrainToNetworkPostfix : __instance was null"); return; }
                Map map = __instance.map;
                //if (map == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::AddNewTerrainToNetworkPostfix : map was null"); return; }
                map.GetComponent<PeopleMoverMapComp>().RegisterMover(c);
            }
        }

        public static bool RemoveTerrainFromNetworkPrefix(IntVec3 c, TerrainGrid __instance, out TerrainState __state)
        {
            int mapIndex = __instance.map.cellIndices.CellToIndex(c);

            __state = new TerrainState(__instance.topGrid[mapIndex], __instance.underGrid[mapIndex]);

            return true;
        }

        public static void RemoveTerrainFromNetworkPostfix(IntVec3 c, TerrainGrid __instance, TerrainState __state)
        {
            //if (__state == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::RemoveTerrainFromNetworkPostfix : __state was null"); return; }
            TerrainDef terrainDef = __state.terrainDef;
            //if (terrainDef == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::RemoveTerrainFromNetworkPostfix : terrainDef was null"); return; }

            if (__state.underTerrainDef != null && terrainDef.defName.Contains("DuneRef_PeopleMover_Terrain"))
            {
                Map map = __instance.map;
                //if (map == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::RemoveTerrainFromNetworkPostfix : map was null"); return; }
                map.GetComponent<PeopleMoverMapComp>().DeregisterMover(c);
            }
        }

        /* Power Comp Patches */

        public static void AddPowerCompToConnectionListPostfix(ref bool __result, ThingDef __instance)
        {
            //if (__instance == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::AddPowerCompToConnectionListPostfix : __instance was null"); return; }

            if (__result != true && !__instance.EverTransmitsPower)
            {
                for (int i = 0; i < __instance.comps.Count; i++)
                {
                    CompProperties comp = __instance.comps[i];
                    //if (comp == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::AddPowerCompToConnectionListPostfix : comp was null"); return; }

                    if (comp.compClass == typeof(PeopleMoverPowerComp))
                    {
                        __result = true;
                    }
                }
            }
        }
   
        /* Clear Cache Patch */

        public static void ClearAllMapsAndWorldPostfix()
        {
            //if (mapsCompCache == null) { Log.Warning($"[DuneRef_PeopleMover] VanillaPatches::ClearAllMapsAndWorldPostfix : mapsCompCache was null"); return; }
            mapsCompCache.Clear();
        }
    }

    // used to hold terrain between RemoveTerrainFromNetworkPrefix and RemoveTerrainFromNetworkPostfix
    public class TerrainState
    {
        public TerrainDef terrainDef;
        public TerrainDef underTerrainDef;

        public TerrainState(TerrainDef terrainDef, TerrainDef underTerrainDef)
        {
            this.terrainDef = terrainDef;
            this.underTerrainDef = underTerrainDef;
        }
    }
}