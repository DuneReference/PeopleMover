using System;

using Verse;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Verse.AI;
using UnityEngine;
using JetBrains.Annotations;
using Verse.Noise;
using System.Reflection;
using System.Security.Cryptography;
using RimWorld;

namespace DuneRef_PeopleMover
{
    public static class VanillaPatches
    {
        public static readonly Type patchType = typeof(VanillaPatches);
        public static Harmony Harm = HarmonyPatches.Harm;

        public static void Patches()
        {
            // Patch CalculatedCostAt to allow PeopleMover's pathcost to make tiles pathcost lower than terrain's pathcost.
            Harm.Patch(AccessTools.Method(typeof(PathGrid), "CalculatedCostAt"), transpiler: new HarmonyMethod(patchType, nameof(ChangePathCostInRepeaterSection)));
            Harm.Patch(AccessTools.Method(typeof(PathGrid), "CalculatedCostAt"), transpiler: new HarmonyMethod(patchType, nameof(ChangePathCostInSnowSection)));

            // Patch CostToMoveIntoCell for resolving speed
            Harm.Patch(
                AccessTools.Method(
                    typeof(Pawn_PathFollower), 
                    "CostToMoveIntoCell", 
                    new Type[] { typeof(Pawn), typeof(IntVec3) }
                ), 
                transpiler: new HarmonyMethod(patchType, nameof(ChangePathCostInEdificeSection)));

            // Patch PathFinder
            // change conveyers pathCost when it calculates buildings.
            Harm.Patch(
                AccessTools.Method(
                    typeof(PathFinder),
                    "GetBuildingCost",
                    new Type[] { typeof(Building), typeof(TraverseParms), typeof(Pawn), typeof(PathFinderCostTuning) }
                ),
                transpiler: new HarmonyMethod(patchType, nameof(ChangePathCostForConveyor)));

            // sanity check to make sure building pathCost doesn't send the current pathCost into the negatives which breaks pathing.
            Harm.Patch(
                AccessTools.Method(
                    typeof(PathFinder),
                    "FindPath",
                    new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms), typeof(PathEndMode), typeof(PathFinderCostTuning) }
                ),
                transpiler: new HarmonyMethod(patchType, nameof(AfterBuildingCostSanityCheck)));

            // Shows the red debug boxes.
            Harm.Patch(
                AccessTools.Method(
                    typeof(PathFinder),
                    "FindPath",
                    new Type[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms), typeof(PathEndMode), typeof(PathFinderCostTuning) }
                ),
                transpiler: new HarmonyMethod(patchType, nameof(PrintPathFinderInfo)));
        }

        public static int GetPathCostFromBuildingRotVsPawnDir(int defaultCost, Rot4 buildingRotation, IntVec3 newCell, IntVec3 prevCell, string methodName, bool forMoveSpeed = false)
        {
            int returningPathCost = defaultCost;

            int costIncrement = (!forMoveSpeed && PeopleMoverSettings.useExplicitPathingPathCost) == true ? PeopleMoverSettings.pathingPathCost : 20;

            if (buildingRotation == Rot4.North)
            {
                if (prevCell.z <= newCell.z)
                {
                    return returningPathCost - costIncrement;
                }
                else
                {
                    return returningPathCost + costIncrement;
                }
            }
            else if (buildingRotation == Rot4.East)
            {
                if (prevCell.x <= newCell.x)
                {
                    return returningPathCost - costIncrement;
                }
                else
                {
                    return returningPathCost + costIncrement;
                }
            }
            else if (buildingRotation == Rot4.South)
            {
                if (prevCell.z >= newCell.z)
                {
                    return returningPathCost - costIncrement;
                }
                else
                {
                    return returningPathCost + costIncrement;
                }
            }
            else if (buildingRotation == Rot4.West)
            {
                if (prevCell.x >= newCell.x)
                {
                    return returningPathCost - costIncrement;
                }
                else
                {
                    return returningPathCost + costIncrement;
                }
            }
            else
            {
                Log.Message($"[{methodName}] None of them? This shouldn't happen. rotation: {buildingRotation}, Coords: prevCell {prevCell.x},?,{prevCell.z} - newCell {newCell.x},?,{newCell.z}");
                return returningPathCost;
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

            return (((building.def.defName == "DuneRef_PeopleMover" || building.def.defName == "DuneRef_PeopleMover_PowerHub") && 
                     building.TryGetComp<CompPowerTrader>().PowerOn
                   ) && buildingPathCost < terrainPathCost) ? buildingPathCost : returningPathCost;
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
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(VanillaPatches.ChangePathCostInSnowSectionFn))))
                    .InstructionEnumeration();
            }
            catch (Exception ex)
            {
                Log.Error($"[DuneRef_PeopleMover] : {ex}");
                return instructions;
            }
        }

        public static int ChangePathCostInSnowSectionFn(int snowPathCost, int buildingPathCost, Thing building)
        {
            return (building != null && (
                    (building.def.defName == "DuneRef_PeopleMover" || building.def.defName == "DuneRef_PeopleMover_PowerHub") && 
                     building.TryGetComp<CompPowerTrader>().PowerOn
                   ) && buildingPathCost < snowPathCost) ? buildingPathCost : snowPathCost;
        }

        /* CostToMoveIntoCell */
        public static IEnumerable<CodeInstruction> ChangePathCostInEdificeSection(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            try
            {
                CodeMatch[] desiredInstructions = new CodeMatch[]{
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
                    .Advance(1)
                    .ThrowIfInvalid("Couldn't find the desired instructions")
                    .Insert(new CodeInstruction(OpCodes.Ldloc_1))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldarg_0))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldarg_1))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(VanillaPatches.ChangePathCostInEdificeSectionFn))))
                    .Advance(1)
                    .RemoveInstructions(4)
                    .InstructionEnumeration();
            }
            catch (Exception ex)
            {
                Log.Error($"[DuneRef_PeopleMover] : {ex}");
                return instructions;
            }
        }

        public static int ChangePathCostInEdificeSectionFn(int currentPathCost, Building edifice, Pawn pawn, IntVec3 newCell)
        {
            if ((edifice.def.defName == "DuneRef_PeopleMover" || edifice.def.defName == "DuneRef_PeopleMover_PowerHub") && edifice.GetComp<CompPowerTrader>().PowerOn)
            {
                int pathCost = PeopleMoverSettings.movespeedPathCost;

                return GetPathCostFromBuildingRotVsPawnDir(pathCost, edifice.Rotation, newCell, pawn.Position, "ChangePathCostInEdificeSection", true);
            } else
            {
                return currentPathCost += edifice.PathWalkCostFor(pawn);
            }
        }

        /* PathFinder */
        public static IEnumerable<CodeInstruction> ChangePathCostForConveyor(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            try
            {
                CodeMatch[] desiredInstructions = new CodeMatch[]{
                    // IL_0198: ldarg.0
                    new CodeMatch(i => i.opcode == OpCodes.Ldarg_0),
                    // IL_0199: ldarg.2
                    new CodeMatch(i => i.opcode == OpCodes.Ldarg_2),
                    // IL_019a: callvirt instance uint16 Verse.Thing::PathFindCostFor(class Verse.Pawn)
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt),
                    // IL_019f: ret
                    new CodeMatch(i => i.opcode == OpCodes.Ret)
                };

                return new CodeMatcher(instructions, generator)
                    .Start()
                    .MatchStartForward(desiredInstructions)
                    .ThrowIfInvalid("Couldn't find the desired instructions")
                    .Advance(3)
                    .Insert(new CodeInstruction(OpCodes.Ldarg_0))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldarg_2))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(VanillaPatches.ChangePathCostForConveyorFn))))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Add))
                    .InstructionEnumeration();
            }
            catch (Exception ex)
            {
                Log.Error($"[DuneRef_PeopleMover] : {ex}");
                return instructions;
            }
        }

        public static int ChangePathCostForConveyorFn(Building building, Pawn pawn)
        {
            if (building != null && (
                 (building.def.defName == "DuneRef_PeopleMover" || building.def.defName == "DuneRef_PeopleMover_PowerHub") && 
                  building.GetComp<CompPowerTrader>().PowerOn)
               )
            {
                return GetPathCostFromBuildingRotVsPawnDir(0, building.Rotation, building.Position, pawn.Position, "ChangePathCostForConveyor");
            }

            return 0;
        }

        public static IEnumerable<CodeInstruction> AfterBuildingCostSanityCheck(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            try
            {
                CodeMatch[] desiredInstructions = new CodeMatch[]{
                    // IL_08d2: ldarg.0
                    new CodeMatch(i => i.opcode == OpCodes.Ldarg_0),
                    // IL_08d3: call instance void Verse.AI.PathFinder::PfProfilerEndSample()
                    new CodeMatch(i => i.opcode == OpCodes.Call),
                    // IL_08d8: br IL_0be1
                    new CodeMatch(i => i.opcode == OpCodes.Br),
                    // IL_08dd: ldloc.s 48
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_S),
                    // IL_08df: ldloc.s 54
                    new CodeMatch(i => i.opcode == OpCodes.Ldloc_S),
                    // IL_08e1: add
                    new CodeMatch(i => i.opcode == OpCodes.Add),
                    // IL_08e2: stloc.s 48
                    new CodeMatch(i => i.opcode == OpCodes.Stloc_S)
                };

                return new CodeMatcher(instructions, generator)
                    .Start()
                    .MatchEndForward(desiredInstructions)
                    .ThrowIfInvalid("Couldn't find the desired instructions")
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Ldloc_S, 48))
                    .Advance(1)
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(VanillaPatches.AfterBuildingCostSanityCheckFn))))
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

        public static int AfterBuildingCostSanityCheckFn(int pathCost)
        {
            return pathCost < 0 ? 0 : pathCost;
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
                    .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(VanillaPatches.PrintPathFinderInfoFn))))
                    .InstructionEnumeration();
            }
            catch (Exception ex)
            {
                Log.Error($"[DuneRef_PeopleMover] : {ex}");
                return instructions;
            }
        }

        public static void PrintPathFinderInfoFn(int mapIndex, int knownCost)
        {
            if (PeopleMoverSettings.showFlashingPathCost)
            {
                Map map = Find.CurrentMap;
                map.debugDrawer.FlashCell(map.cellIndices.IndexToCell(mapIndex), knownCost, knownCost.ToString());
            }
        }
    }
}