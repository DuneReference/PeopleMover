using UnityEngine;
using Verse;

namespace DuneRef_PeopleMover
{
    [StaticConstructorOnStartup]
    public static class DuneRef_Textures
    {
        static DuneRef_Textures()
        {
            Arrow = ContentFinder<Texture2D>.Get("Things/Buildings/PeopleMover/PlaceWorker_MultiDirectional_Arrow", true);
        }

        public static readonly Texture2D Arrow;
    }
}
