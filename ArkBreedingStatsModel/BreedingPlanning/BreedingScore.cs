﻿using System;
using System.Collections.Generic;
using System.Linq;
using ArkBreedingStatsModel.Ark;
using ArkBreedingStatsModel.Library;
using ArkBreedingStatsModel.Species;
using ArkBreedingStatsModel.Values;

namespace ArkBreedingStatsModel.BreedingPlanning
{

    public enum BreedingMode
    {
        BestNextGen,
        TopStatsLucky,
        TopStatsConservative
    }

    /// <summary>
    /// Calculation of a breeding score, which will help to decide which pairings will result in a desired offspring.
    /// </summary>
    public static class BreedingScore
    {
        /// <summary>
        /// Calculates the breeding score of all possible pairs.
        /// </summary>
        /// <param name="females"></param>
        /// <param name="males"></param>
        /// <param name="species"></param>
        /// <param name="bestPossLevels"></param>
        /// <param name="statWeights"></param>
        /// <param name="bestLevels"></param>
        /// <param name="breedingMode"></param>
        /// <param name="considerChosenCreature"></param>
        /// <param name="considerMutationLimit"></param>
        /// <param name="mutationLimit"></param>
        /// <param name="creaturesMutationsFilteredOut"></param>
        /// <param name="offspringLevelLimit">If &gt; 0, pairs that can result in a creature with a level higher than that, are highlighted. This can be used if there's a level cap.</param>
        /// <param name="downGradeOffspringWithLevelHigherThanLimit">Downgrade score if level is higher than limit.</param>
        /// <param name="onlyBestSuggestionForFemale">Only the pairing with the highest score is kept for each female. Is not used if species has no sex or sex is ignored in breeding planner.</param>
        /// <returns></returns>
        public static List<BreedingPair> CalculateBreedingScores(Creature[] females, Creature[] males, Species.Species species,
            short[] bestPossLevels, double[] statWeights, int[] bestLevels, BreedingMode breedingMode,
            bool considerChosenCreature, bool considerMutationLimit, int mutationLimit,
            ref bool creaturesMutationsFilteredOut, int offspringLevelLimit = 0, bool downGradeOffspringWithLevelHigherThanLimit = false,
            bool onlyBestSuggestionForFemale = false, bool ignoreSex = true, bool considerOnlyEvenForHighStats = false)
        {
            var breedingPairs = new List<BreedingPair>();
            ignoreSex = ignoreSex || species.noGender;
            for (int fi = 0; fi < females.Length; fi++)
            {
                var female = females[fi];
                for (int mi = 0; mi < males.Length; mi++)
                {
                    var male = males[mi];
                    // if ignoreSex (useful when using S+ mutator), skip pair if
                    // creatures are the same, or pair has already been added
                    if (ignoreSex)
                    {
                        if (considerChosenCreature)
                        {
                            if (male == female)
                                continue;
                        }
                        else if (fi == mi)
                            break;
                    }
                    // if mutation limit is set, only skip pairs where both parents exceed that limit. One parent is enough to trigger a mutation.
                    if (considerMutationLimit && female.Mutations > mutationLimit && male.Mutations > mutationLimit)
                    {
                        creaturesMutationsFilteredOut = true;
                        continue;
                    }

                    double t = 0;
                    int nrTS = 0;
                    double eTS = 0;

                    int topFemale = 0;
                    int topMale = 0;

                    int maxPossibleOffspringLevel = 1;

                    for (int s = 0; s < Values.Values.STATS_COUNT; s++)
                    {
                        if (s == (int)StatNames.Torpidity || !species.UsesStat(s)) continue;
                        bestPossLevels[s] = 0;
                        int higherLevel = Math.Max(female.levelsWild[s], male.levelsWild[s]);
                        int lowerLevel = Math.Min(female.levelsWild[s], male.levelsWild[s]);
                        if (higherLevel < 0) higherLevel = 0;
                        if (lowerLevel < 0) lowerLevel = 0;
                        maxPossibleOffspringLevel += higherLevel;

                        bool ignoreTopStats = considerOnlyEvenForHighStats
                                              && higherLevel % 2 != 0
                                              && statWeights[s] > 0;

                        bool higherIsBetter = statWeights[s] >= 0;

                        double tt = statWeights[s] * (GameConstants.ProbabilityHigherLevel * higherLevel + GameConstants.ProbabilityLowerLevel * lowerLevel) / 40;
                        if (tt != 0)
                        {
                            if (breedingMode == BreedingMode.TopStatsLucky)
                            {
                                if (!ignoreTopStats && (female.levelsWild[s] == bestLevels[s] || male.levelsWild[s] == bestLevels[s]))
                                {
                                    if (female.levelsWild[s] == bestLevels[s] && male.levelsWild[s] == bestLevels[s])
                                        tt *= 1.142;
                                }
                                else if (bestLevels[s] > 0)
                                    tt *= .01;
                            }
                            else if (breedingMode == BreedingMode.TopStatsConservative && bestLevels[s] > 0)
                            {
                                bestPossLevels[s] = (short)(higherIsBetter ? Math.Max(female.levelsWild[s], male.levelsWild[s]) : Math.Min(female.levelsWild[s], male.levelsWild[s]));
                                tt *= .01;
                                if (!ignoreTopStats && (female.levelsWild[s] == bestLevels[s] || male.levelsWild[s] == bestLevels[s]))
                                {
                                    nrTS++;
                                    eTS += female.levelsWild[s] == bestLevels[s] && male.levelsWild[s] == bestLevels[s] ? 1 : GameConstants.ProbabilityHigherLevel;
                                    if (female.levelsWild[s] == bestLevels[s])
                                        topFemale++;
                                    if (male.levelsWild[s] == bestLevels[s])
                                        topMale++;
                                }
                            }
                        }
                        t += tt;
                    }

                    if (breedingMode == BreedingMode.TopStatsConservative)
                    {
                        if (topFemale < nrTS && topMale < nrTS)
                            t += eTS;
                        else
                            t += .1 * eTS;
                        // check if the best possible stat outcome regarding topLevels already exists in a male
                        bool maleExists = false;

                        foreach (Creature cr in males)
                        {
                            maleExists = true;
                            for (int s = 0; s < Values.Values.STATS_COUNT; s++)
                            {
                                if (s == (int)StatNames.Torpidity
                                    || !cr.Species.UsesStat(s)
                                    || cr.levelsWild[s] == bestPossLevels[s]
                                    || bestPossLevels[s] != bestLevels[s])
                                    continue;

                                maleExists = false;
                                break;
                            }
                            if (maleExists)
                                break;
                        }
                        if (maleExists)
                            t *= .4; // another male with the same stats is not worth much, the mating-cooldown of males is short.
                        else
                        {
                            // check if the best possible stat outcome already exists in a female
                            bool femaleExists = false;
                            foreach (Creature cr in females)
                            {
                                femaleExists = true;
                                for (int s = 0; s < Values.Values.STATS_COUNT; s++)
                                {
                                    if (s == (int)StatNames.Torpidity
                                        || !cr.Species.UsesStat(s)
                                        || cr.levelsWild[s] == bestPossLevels[s]
                                        || bestPossLevels[s] != bestLevels[s])
                                        continue;

                                    femaleExists = false;
                                    break;
                                }
                                if (femaleExists)
                                    break;
                            }
                            if (femaleExists)
                                t *= .8; // another female with the same stats may be useful, but not so much in conservative breeding
                        }
                        //t *= 2; // scale conservative mode as it rather displays improvement, but only scarcely
                    }

                    var highestOffspringOverLevelLimit =
                        offspringLevelLimit > 0 && offspringLevelLimit < maxPossibleOffspringLevel;
                    if (highestOffspringOverLevelLimit && downGradeOffspringWithLevelHigherThanLimit)
                        t *= 0.01;

                    int mutationPossibleFrom = female.Mutations < GameConstants.MutationPossibleWithLessThan && male.Mutations < GameConstants.MutationPossibleWithLessThan ? 2
                        : female.Mutations < GameConstants.MutationPossibleWithLessThan || male.Mutations < GameConstants.MutationPossibleWithLessThan ? 1 : 0;

                    breedingPairs.Add(new BreedingPair(female, male,
                        t * 1.25,
                        (mutationPossibleFrom == 2 ? GameConstants.ProbabilityOfOneMutation : mutationPossibleFrom == 1 ? GameConstants.ProbabilityOfOneMutationFromOneParent : 0),
                        highestOffspringOverLevelLimit));
                }
            }

            breedingPairs = breedingPairs.OrderByDescending(p => p.BreedingScore).ToList();

            if (onlyBestSuggestionForFemale && !ignoreSex)
            {
                var onlyOneSuggestionPerFemale = new List<BreedingPair>();
                foreach (var bp in breedingPairs)
                {
                    if (!onlyOneSuggestionPerFemale.Any(p => p.Mother == bp.Mother))
                        onlyOneSuggestionPerFemale.Add(bp);
                }

                breedingPairs = onlyOneSuggestionPerFemale;
            }

            return breedingPairs;
        }

        public static void SetBestLevels(IEnumerable<Creature> creatures, int[] bestLevels, double[] statWeights)
        {
            for (int s = 0; s < Values.Values.STATS_COUNT; s++)
                bestLevels[s] = -1;

            foreach (Creature c in creatures)
            {
                for (int s = 0; s < Values.Values.STATS_COUNT; s++)
                {
                    if ((s == (int)StatNames.Torpidity || statWeights[s] >= 0) && c.levelsWild[s] > bestLevels[s])
                        bestLevels[s] = c.levelsWild[s];
                    else if (s != (int)StatNames.Torpidity && statWeights[s] < 0 && c.levelsWild[s] >= 0 && (c.levelsWild[s] < bestLevels[s] || bestLevels[s] < 0))
                        bestLevels[s] = c.levelsWild[s];
                }
            }
        }
    }
}
