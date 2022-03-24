using Newtonsoft.Json;
using System.Collections.Generic;

namespace ArkBreedingStatsModel.Species
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class ColorRegion
    {
        [JsonProperty]
        public string name;
        /// <summary>
        /// List of natural occuring color names.
        /// </summary>
        [JsonProperty]
        public List<string> colors;
        /// <summary>
        /// List of natural occuring ARKColors.
        /// </summary>
        public List<ArkColor> naturalColors;

        public ColorRegion()
        {
            name = "Unknown";
        }

        /// <summary>
        /// Sets the ARKColor objects
        /// </summary>
        internal void Initialize(ArkColors arkColors)
        {
            if (colors == null) return;
            naturalColors = new List<ArkColor>();
            foreach (var c in colors)
            {
                ArkColor cl = arkColors.ByName(c);
                if (cl.Hash != 0 && !naturalColors.Contains(cl))
                    naturalColors.Add(cl);
            }
        }
    }
}
