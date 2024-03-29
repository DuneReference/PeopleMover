﻿using UnityEngine;
using Verse;

namespace DuneRef_PeopleMover
{
    [StaticConstructorOnStartup]
    public class PlaceWorker_Arrow : PlaceWorker
    {
        public static readonly Material arrow;
        static PlaceWorker_Arrow()
        {
            arrow = FadedMaterialPool.FadedVersionOf(MaterialPool.MatFrom(DuneRef_Textures.Arrow), .9f);
        }
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            base.DrawGhost(def, center, rot, ghostCol, thing);
            var pos = center.ToVector3Shifted();
            pos.y = AltitudeLayer.LightingOverlay.AltitudeFor();
            Graphics.DrawMesh(MeshPool.plane10, pos, rot.AsQuat, arrow, 0);
        }
    }
}
