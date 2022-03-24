﻿using System;
using System.Collections.Generic;

namespace ArkBreedingStatsModel.Species
{
    [Serializable]
    public class Kibble : Dictionary<string, int>
    {
        public string RecipeAsText()
        {
            string result = "";

            foreach (string s in Keys)
            {
                result += $"\n {this[s]} × {s}";
            }

            return result;
        }
    }
}
