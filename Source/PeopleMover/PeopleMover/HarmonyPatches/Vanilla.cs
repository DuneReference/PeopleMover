using System;

using Verse;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Verse.AI;
using System.Reflection;
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
            Harm.Patch(AccessTools.Method(typeof(PathGrid), "CalculatedCostAt"), transpiler: new HarmonyMethod(patchType, nameof(ChangePathCostInSnowSection)));

            // Patch CostToMoveIntoCell for changing speed of pawn on the mover
            Harm.Patch(
                AccessTools.Method(
                    typeof(Pawn_PathFollower), 
                    "CostToMoveIntoCell", 
                    new Type[] { typeof(Pawn), typeof(IntVec3) }
                ), 
                transpiler: new HarmonyMethod(patchType, nameof(ChangePathCostInEdificeSection)));

            // Patch PathFinder to prioritize moving on correct direction movers, and do the opposite when not the correct direction.
            Harm.Patch(
                AccessTools.Method(
                    typeof(PathFinder),
                    "FindPath",
                    new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms), typeof(PathEndMode), typeof(PathFinderCostTuning) }
                ),
                transpiler: new HarmonyMethod(patchType, nameof(ChangePathCostForMover)));

            // Shows the red debug boxes.
            Harm.Patch(
                AccessTools.Method(
                    typeof(PathFinder),
                    "FindPath",
                    new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms), typeof(PathEndMode), typeof(PathFinderCostTuning) }
                ),
                transpiler: new HarmonyMethod(patchType, nameof(PrintPathFinderInfo)));

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
        public static int GetPathCostFromBuildingRotVsPawnDir(int defaultCost, Rot4 buildingRotation, IntVec3 newCell, IntVec3 prevCell, string methodName, bool forMoveSpeed = false)
        {
            int returningPathCost = defaultCost;

            int costIncrement = (!forMoveSpeed && PeopleMoverSettings.useExplicitPathingPathCost) == true ? PeopleMoverSettings.pathingPathCost : 20;

            if (PeopleMoverSettings.omniMover) return returningPathCost - costIncrement;

            if (buildingRotation == Rot4.North)
            {
                if (prevCell.z <= newCell.z)
                {
                    returningPathCost -= costIncrement;
                }
                else
                {
                    returningPathCost += costIncrement;
                }
            }
            else if (buildingRotation == Rot4.East)
            {
                if (prevCell.x <= newCell.x)
                {
                    returningPathCost -= costIncrement;
                }
                else
                {
                    returningPathCost += costIncrement;
                }
            }
            else if (buildingRotation == Rot4.South)
            {
                if (prevCell.z >= newCell.z)
                {
                    returningPathCost -= costIncrement;
                }
                else
                {
                    returningPathCost += costIncrement;
                }
            }
            else if (buildingRotation == Rot4.West)
            {
                if (prevCell.x >= newCell.x)
                {
                    returningPathCost -= costIncrement;
                }
                else
                {
                    returningPathCost += costIncrement;
                }
            }
            else
            {
                Log.Message($"[{methodName}] None of them? This shouldn't happen. rotation: {buildingRotation}, Coords: prevCell {prevCell.x},?,{prevCell.z} - newCell {newCell.x},?,{newCell.z}");
                return returningPathCost;
            }

            return returningPathCost;
        }

        public static bool IsMapIndexApartOfPeopleMoverPowerHubNetwork(IntVec3 cell, Map map)
        {
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
            try
            {
                CodeMatch[] desiredInstructions = new CodeMatch[]{
                    // IL_00c8: ble.s IL_00cd
                    new CodeMatch(i => i.opcode == OpCodes.Ble_S),
                    // IL_00ca: ldloc.s 7
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_S),
                    // IL_00cc: stloc.0
                    new CodeMatch(i => i.opcode == OpCodes.Stloc_0),
                    // IL_00cd: ldloc.s 6
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_S),
                    // IL_00cf: isinst RimWorld.Building_Door
                    new CodeMatch(i => i.opcode == OpCodes.Isinst),
                    // IL_00d4: brfalse.s IL_00fc
                    new CodeMatch(i => i.opcode == OpCodes.Brfalse_S),
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

            if ((building.def.defName == "DuneRef_PeopleMover" || building.def.defName == "DuneRef_PeopleMover_PowerHub") && building.TryGetComp<PeopleMoverPowerComp>().PowerOn)
            {
                if (buildingPathCost < terrainPathCost)
                {
                    returningPathCost = buildingPathCost;
                }
            }

            return returningPathCost;
        }

        public static IEnumerable<CodeInstruction> ChangePathCostInSnowSection(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            try
            {
                CodeMatch[] desiredInstructions = new CodeMatch[]{
                    // IL_011b: callvirt instance valuetype Verse.SnowCategory Verse.SnowGrid::GetCategory(valuetype Verse.IntVec3)
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt),
                    // IL_0120: call int32 Verse.SnowUtility::MovementTicksAddOn(valuetype Verse.SnowCategory)
                    new CodeMatch(i => i.opcode == OpCodes.Call),
                    // IL_0125: stloc.s 4
                    new CodeMatch(i => i.opcode == OpCodes.Stloc_S),
                    // IL_0127: ldloc.s 4
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_S),
                    // IL_0129: ldloc.0
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_0),
                    // IL_012a: ble.s IL_012f
                    new CodeMatch(i => i.opcode == OpCodes.Ble_S),
                    // IL_012c: ldloc.s 4
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_S)
                };

                return new CodeMatcher(instructions, generator)
                    .Start()
                    .MatchEndForward(desiredInstructions)
                    .Advance(1)
                    .ThrowIfInvalid("Couldn't find the desired instructions")
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
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(VanillaPatches.ChangePathCostInSnowSectionFn))))
                    .InstructionEnumeration();
            }
            catch (Exception ex)
            {
                Log.Error($"[DuneRef_PeopleMover] : {ex}");
                return instructions;
            }
        }

        public static int ChangePathCostInSnowSectionFn(int snowPathCost, int runningPathCost, Thing building, TerrainDef terrain, PathGrid pathGrid, IntVec3 nextCell)
        {
            int returningPathCost = snowPathCost;

            if (building != null)
            {
                if (building.def.defName == "DuneRef_PeopleMover" || building.def.defName == "DuneRef_PeopleMover_PowerHub")
                {
                    if (building.TryGetComp<PeopleMoverPowerComp>().PowerOn)
                    {
                        returningPathCost = runningPathCost;
                    }
                }
            } else
            {
                if (terrain.defName == "DuneRef_PeopleMover_Terrain")
                {
                    if (IsMapIndexApartOfPeopleMoverPowerHubNetwork(nextCell, pathGrid.map))
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
            try
            {
                CodeMatch[] desiredInstructions = new CodeMatch[]{
                    // IL_0069: ldloc.1
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_1),
                    // IL_006a: brfalse.s IL_0076
                    new CodeMatch(i => i.opcode == OpCodes.Brfalse_S),
                    // IL_006c: ldloc.0
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_0),
                    // IL_006d: ldloc.1
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_1),
                    // IL_006e: ldarg.0
                    new CodeMatch(i => i.opcode == OpCodes.Ldarg_0),
                    // IL_006f: callvirt instance uint16 Verse.Building::PathWalkCostFor(class Verse.Pawn)
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt),
                    // IL_0074: add
                    new CodeMatch(i => i.opcode == OpCodes.Add),
                    // IL_0075: stloc.0
                    new CodeMatch(i => i.opcode == OpCodes.Stloc_0)
                };

                return new CodeMatcher(instructions, generator)
                    .Start()
                    .MatchStartForward(desiredInstructions)
                    .ThrowIfInvalid("Couldn't find the desired instructions")
                    .RemoveInstructions(7)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_0))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_1))
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

        public static int ChangePathCostInEdificeSectionFn(int currentPathCost, Building building, Pawn pawn, IntVec3 newCell)
        {
            int returningPathCost = currentPathCost;

            if (building != null) 
            {
                if ((building.def.defName == "DuneRef_PeopleMover" || building.def.defName == "DuneRef_PeopleMover_PowerHub") && building.GetComp<PeopleMoverPowerComp>().PowerOn)
                {
                    int pathCost = PeopleMoverSettings.movespeedPathCost;

                    returningPathCost =  GetPathCostFromBuildingRotVsPawnDir(pathCost, building.Rotation, newCell, pawn.Position, "ChangePathCostInEdificeSection", true);
                }
                else
                {
                    returningPathCost += building.PathWalkCostFor(pawn);
                }
            } else
            {
                TerrainDef terrain = pawn.Map.terrainGrid.TerrainAt(newCell);

                if (terrain.defName.Contains("DuneRef_PeopleMover_Terrain"))
                {
                    if (IsMapIndexApartOfPeopleMoverPowerHubNetwork(newCell, pawn.Map))
                    {
                        int pathCost = PeopleMoverSettings.movespeedPathCost;

                        Rot4 rotation = Rot4.North;

                        if (terrain.defName.Contains("East"))
                        {
                            rotation = Rot4.East;
                        }
                        else if (terrain.defName.Contains("South"))
                        {
                            rotation = Rot4.South;
                        }
                        else if (terrain.defName.Contains("West"))
                        {
                            rotation = Rot4.West;
                        }

                        returningPathCost = GetPathCostFromBuildingRotVsPawnDir(pathCost, rotation, newCell, pawn.Position, "ChangePathCostInEdificeSection", true);
                    }
                }
            }

            return returningPathCost;
        }

        /* PathFinder */
        public static IEnumerable<CodeInstruction> ChangePathCostForMover(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            try
            {
                CodeMatch[] desiredInstructions = new CodeMatch[]{
                    // IL_08d2: ldarg.0
                    new CodeMatch(i => i.opcode == OpCodes.Ldarg_0),
                    // IL_09f3: ldfld class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']RimWorld.Blueprint>[] ['Assembly-CSharp']Verse.AI.PathFinder::blueprintGrid
                    new CodeMatch(i => i.opcode == OpCodes.Ldfld),
                    // IL_09f8: ldloc.s 45
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_S),
                    // IL_09fa: ldelem.ref
                    new CodeMatch(i => i.opcode == OpCodes.Ldelem_Ref),
                    // IL_09fb: stloc.s 55
                    new CodeMatch(i => i.opcode == OpCodes.Stloc_S),
                    // IL_09fd: ldloc.s 55
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_S),
                    // IL_09ff: brfalse IL_0a6a
                    new CodeMatch(i => i.opcode == OpCodes.Brfalse_S),
                    // IL_0a04: ldarg.0
                    new CodeMatch(i => i.opcode == OpCodes.Ldarg_0),
                    // IL_0a05: ldstr "Blueprints"
                    new CodeMatch(i => i.opcode == OpCodes.Ldstr),
                    // IL_0a0a: call instance void ['Assembly-CSharp']Verse.AI.PathFinder::PfProfilerBeginSample(string)
                    new CodeMatch(i => i.opcode == OpCodes.Call),
                    // IL_0a0f: ldc.i4.0
                    new CodeMatch(i => i.opcode == OpCodes.Ldc_I4_0)
                };

                return new CodeMatcher(instructions, generator)
                    .Start()
                    .MatchStartForward(desiredInstructions)
                    .ThrowIfInvalid("Couldn't find the desired instructions")
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_S, 48))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_S, 45))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_S, 53))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_0))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldarg_2))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(VanillaPatches.ChangePathCostForMoverFn))))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Stloc_S, 48))
                    .InstructionEnumeration();
            }
            catch (Exception ex)
            {
                Log.Error($"[DuneRef_PeopleMover] : {ex}");
                return instructions;
            }
        }

        public static int ChangePathCostForMoverFn(int pathCost, int mapIndex, Building building, Pawn pawn, LocalTargetInfo dest)
        {
            int returningPathCost = pathCost;

            if (building != null)
            {
                if ((building.def.defName == "DuneRef_PeopleMover" || building.def.defName == "DuneRef_PeopleMover_PowerHub") && building.GetComp<PeopleMoverPowerComp>().PowerOn)
                {
                    returningPathCost =  GetPathCostFromBuildingRotVsPawnDir(returningPathCost, building.Rotation, building.Position, pawn.Position, "ChangePathCostForMover_Building");
                }
            } else if (pawn != null)
            {
                TerrainDef terrain = pawn.Map.terrainGrid.topGrid[mapIndex];

                if (terrain.defName.Contains("DuneRef_PeopleMover_Terrain"))
                {
                    if (IsMapIndexApartOfPeopleMoverPowerHubNetwork(pawn.Map.cellIndices.IndexToCell(mapIndex), pawn.Map))
                    {
                        Rot4 rotation = Rot4.North;

                        if (terrain.defName.Contains("East"))
                        {
                            rotation = Rot4.East;
                        }
                        else if (terrain.defName.Contains("South"))
                        {
                            rotation = Rot4.South;
                        }
                        else if (terrain.defName.Contains("West"))
                        {
                            rotation = Rot4.West;
                        }

                        IntVec3 terrainCell = pawn.Map.cellIndices.IndexToCell(mapIndex);
                        returningPathCost = GetPathCostFromBuildingRotVsPawnDir(returningPathCost, rotation, terrainCell, pawn.Position, "ChangePathCostForMover_Terrain");
                    }
                }
            }

            return returningPathCost < 0 ? 0 : returningPathCost;
        }

        public static IEnumerable<CodeInstruction> PrintPathFinderInfo(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            try
            {
                CodeMatch[] desiredInstructions = new CodeMatch[]{
                    // IL_0bcf: add
                    new CodeMatch(i => i.opcode == OpCodes.Add),
                    // IL_0bd0: stloc.s 15
                    new CodeMatch(i => i.opcode == OpCodes.Stloc_S),
                    // IL_0bd2: ldarg.0
                    new CodeMatch(i => i.opcode == OpCodes.Ldarg_0),
                    // IL_0bd3: ldfld class System.Collections.Generic.PriorityQueue`2<int32, int32> Verse.AI.PathFinder::openList
                    new CodeMatch(i => i.opcode == OpCodes.Ldfld),
                    // IL_0bd8: ldloc.s 45
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_S),
                    // IL_0bda: ldloc.s 51
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_S),
                    // IL_0bdc: callvirt instance void class System.Collections.Generic.PriorityQueue`2<int32, int32>::Enqueue(!0, !1)
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt)

                };

                return new CodeMatcher(instructions, generator)
                    .Start()
                    .MatchEndForward(desiredInstructions)
                    .ThrowIfInvalid("Couldn't find the desired instructions")
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_S, 45))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_S, 49))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_0))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(VanillaPatches.PrintPathFinderInfoFn))))
                    .InstructionEnumeration();
            }
            catch (Exception ex)
            {
                Log.Error($"[DuneRef_PeopleMover] : {ex}");
                return instructions;
            }
        }

        public static void PrintPathFinderInfoFn(int mapIndex, int knownCost, Pawn pawn)
        {
            if (PeopleMoverSettings.showFlashingPathCost)
            {
                pawn.Map.debugDrawer.FlashCell(pawn.Map.cellIndices.IndexToCell(mapIndex), knownCost, knownCost.ToString());
            }
        }

        /* Terrain Patches */
        public static void AddNewTerrainToNetworkPostfix(IntVec3 c, TerrainDef newTerr, TerrainGrid __instance)
        {
            if (newTerr.defName.Contains("DuneRef_PeopleMover_Terrain"))
            {
                __instance.map.GetComponent<PeopleMoverMapComp>().RegisterMover(c);
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
            if (__state.underTerrainDef != null && __state.terrainDef.defName.Contains("DuneRef_PeopleMover_Terrain"))
            {
                __instance.map.GetComponent<PeopleMoverMapComp>().DeregisterMover(c);
            }
        }

        /* Power Comp Patches */

        public static void AddPowerCompToConnectionListPostfix(ref bool __result, ThingDef __instance)
        {
            if(__result != true && !__instance.EverTransmitsPower)
            {
                for (int i = 0; i < __instance.comps.Count; i++)
                {
                    if (__instance.comps[i].compClass == typeof(PeopleMoverPowerComp))
                    {
                        __result = true;
                    }
                }
            }
        }
   
        /* Clear Cache Patch */

        public static void ClearAllMapsAndWorldPostfix()
        {
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