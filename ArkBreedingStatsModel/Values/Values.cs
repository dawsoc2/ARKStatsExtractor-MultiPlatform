﻿//using ArkBreedingStatsModel.Library;
using ArkBreedingStatsModel.Mods;
using ArkBreedingStatsModel.Species;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;

namespace ArkBreedingStatsModel.Values
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Values
    {
        public const int STATS_COUNT = 12;

        /// <summary>
        /// Checks if the version string is a format version that is supported by the version of this application.
        /// </summary>
        private static bool IsValidFormatVersion(string version) =>
            version == "1.12" // format with 12 stats (minimum required format)
            || version == "1.13" // introduced remaps for blueprintPaths
            || version == "1.14-flyerspeed"; // introduced isFlyer property for AllowFlyerSpeedLeveling

        private static Values _V;

        [JsonProperty]
        private string version = "0.0";
        /// <summary>
        /// Must be present and a supported value. Defaults to an invalid value
        /// </summary>
        [JsonProperty]
        private string format = string.Empty;
        public Version Version = new Version(0, 0);
        public Version modVersion = new Version(0, 0);
        [JsonProperty]
        public List<Species.Species> species = new List<Species.Species>();
        [JsonProperty]
        public List<List<object>> colorDefinitions;
        [JsonProperty]
        public List<List<object>> dyeDefinitions;
        /// <summary>
        /// If a species for a blueprintPath is requested, the blueprintPath will be remapped if an according key is present.
        /// This is needed if species are remapped ingame, e.g. if a variant is removed.
        /// </summary>
        [JsonProperty("remaps")]
        private Dictionary<string, string> blueprintRemapping;

        public ArkColors Colors;
        public ArkColors Dyes;

        public List<string> speciesNames = new List<string>();
        internal Dictionary<string, string> aliases;
        public List<string> speciesWithAliasesList;
        private Dictionary<string, Species.Species> blueprintToSpecies;
        private Dictionary<string, Species.Species> nameToSpecies;
        private Dictionary<string, Species.Species> classNameToSpecies;
        /// <summary>
        /// Representing the current server multipliers except statMultipliers. Also considers event-changes.
        /// </summary>
        public ServerMultipliers currentServerMultipliers;
        
        /// <summary>
        /// List of presets for server multipliers for easier setting. Also contains the singleplayer multipliers.
        /// </summary>
        //public ServerMultipliersPresets serverMultipliersPresets;

        /// <summary>
        /// The default food data used for taming. Specific species can override it.
        /// </summary>
        public Dictionary<string, TamingFood> defaultFoodData;

        /// <summary>
        /// The special food data for species used for taming. Saved to use for loaded mods.
        /// </summary>
        public Dictionary<string, TamingData> specialFoodData;

        /// <summary>
        /// Infos about the available mod values
        /// </summary>
        //public ModsManifest modsManifest;

        /// <summary>
        /// If this represents values for a mod, the mod-infos are found here.
        /// </summary>
        [JsonProperty]
        public Mod mod;

        /// <summary>
        /// Contains all species-classes that should be ignored when importing a savegame.
        /// This is e.g. used to filter out rafts which are species in ARK.
        /// </summary>
        private List<string> ignoreSpeciesClassesOnImport;

        /// <summary>
        /// For the main-values object this hash represents the current loaded mods and their order.
        /// </summary>
        public int loadedModsHash;

        /// <summary>
        /// Hash if no mod is loaded.
        /// </summary>
        //public static int NoModsHash = CreatureCollection.CalculateModListHash(new List<Mod>());

        /// <summary>
        /// Returns the stat-index for the given order index (like it is ordered ingame).
        /// </summary>
        public static readonly int[] statsDisplayOrder = {
            (int)StatNames.Health,
            (int)StatNames.Stamina,
            (int)StatNames.Oxygen,
            (int)StatNames.Food,
            (int)StatNames.Water,
            (int)StatNames.Temperature,
            (int)StatNames.Weight,
            (int)StatNames.MeleeDamageMultiplier,
            (int)StatNames.SpeedMultiplier,
            (int)StatNames.TemperatureFortitude,
            (int)StatNames.CraftingSpeedMultiplier,
            (int)StatNames.Torpidity
            };

        public static Values V => _V ?? (_V = new Values());

        /// <summary>
        /// Loads the values from the default file.
        /// </summary>
        /// <returns></returns>
        public Values LoadValues()
        {
            _V = LoadValuesFile(FileService.GetJsonPath(FileService.ValuesFolder, FileService.ValuesJson));
            // TODO: Uncomment these methods
            //InitializeStatValues();
            return _V;
        }

        // private void InitializeStatValues()
        // {
        //     bool setTamingFood = TamingFoodData.TryLoadDefaultFoodData(out specialFoodData);
        //     if (specialFoodData == null) specialFoodData = new Dictionary<string, TamingData>();
        //     _V.specialFoodData = specialFoodData;

        //     const string defaultFoodNameKey = "default";
        //     if (setTamingFood && specialFoodData.ContainsKey(defaultFoodNameKey))
        //     {
        //         _V.defaultFoodData = specialFoodData[defaultFoodNameKey].specialFoodValues;
        //     }
        //     else
        //     {
        //         _V.defaultFoodData = new Dictionary<string, TamingFood>();
        //     }

        //     //var speciesWoFoodData = new List<string>(); // to determine which species has no food data yet
        //     _V.speciesNames = new List<string>();
        //     foreach (Species sp in _V.species)
        //     {
        //         _V.speciesNames.Add(sp.name);
        //         if (setTamingFood && specialFoodData.ContainsKey(sp.name))
        //         {
        //             sp.taming.eats = specialFoodData[sp.name].eats;
        //             sp.taming.eatsAlsoPostTame = specialFoodData[sp.name].eatsAlsoPostTame;
        //             sp.taming.specialFoodValues = specialFoodData[sp.name].specialFoodValues;
        //         }
        //         //if (sp.IsDomesticable && !specialFoodData.ContainsKey(sp.name)) speciesWoFoodData.Add(sp.name);
        //     }
        //     //System.Windows.Forms.Clipboard.SetText(speciesWoFoodData.Any() ? string.Join("\n", speciesWoFoodData) : string.Empty);

        //     OrderSpeciesAndApplyCustomVariants();

        //     _V.LoadAliases();
        //     _V.UpdateSpeciesBlueprintDictionaries();
        //     _V.loadedModsHash = NoModsHash;

        //     // transfer extra loaded objects from the old object to the new one
        //     _V.modsManifest = modsManifest;
        //     _V.serverMultipliersPresets = serverMultipliersPresets;
        // }

        private static Values LoadValuesFile(string filePath)
        {
            if (FileService.LoadJsonFile(filePath, out Values readData, out string errorMessage))
            {
                if (!IsValidFormatVersion(readData.format)) throw new FormatException($"Unsupported values format version: {(readData.format ?? "null")}");
                return readData;
            }
            throw new FileLoadException(errorMessage);
        }

        /// <summary>
        /// Tries to load a mod file.
        /// If the mod-values will be used, setModFileName should be true.
        /// If the file cannot be found or the format is wrong, the file is ignored and no exception is thrown if throwExceptionOnFail is false.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="setModFileName"></param>
        /// <param name="throwExceptionOnFail"></param>
        /// <param name="values"></param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public static bool TryLoadValuesFile(string filePath, bool setModFileName, bool throwExceptionOnFail, out Values values, out string errorMessage)
        {
            values = null;
            errorMessage = null;
            try
            {
                values = LoadValuesFile(filePath);
                if (setModFileName) values.mod.FileName = Path.GetFileName(filePath);
                return true;
            }
            catch (FileNotFoundException ex)
            {
                errorMessage = "Values-File '" + filePath + "' not found. "
                             + "This collection seems to have modified stat values that are saved in a separate file, "
                             + "which couldn't be found at the saved location.";
                if (throwExceptionOnFail)
                    throw new FileNotFoundException(errorMessage, ex);
            }
            catch (FormatException ex)
            {
                errorMessage = "Values-File '" + filePath + $"' has an invalid version.\n{ex.Message}\nTry updating ARK Smart Breeding.";
                if (throwExceptionOnFail)
                    throw new FormatException(errorMessage);
            }
            return false;
        }

        /// <summary>
        /// Loads extra values-files that can add species values or modify existing ones
        /// </summary>
        // public bool LoadModValues(List<string> modValueFileNames, bool throwExceptionOnFail, out List<Mod> mods, out string resultsMessage)
        // {
        //     loadedModsHash = 0;
        //     List<Values> modifiedValues = new List<Values>();
        //     mods = new List<Mod>();
        //     resultsMessage = null;
        //     if (modValueFileNames == null) return false;

        //     StringBuilder resultsMessageSB = new StringBuilder();
        //     foreach (string mf in modValueFileNames)
        //     {
        //         string filename = FileService.GetJsonPath(Path.Combine(FileService.ValuesFolder, mf));

        //         if (TryLoadValuesFile(filename, setModFileName: true, false, out Values modValues, out string modFileErrorMessage))
        //         {
        //             modifiedValues.Add(modValues);
        //         }
        //         else if (!string.IsNullOrEmpty(modFileErrorMessage))
        //         {
        //             resultsMessageSB.AppendLine(modFileErrorMessage);
        //         }
        //     }

        //     int speciesAdded = 0;
        //     // update data if existing
        //     foreach (Values modValues in modifiedValues)
        //     {
        //         // if mods are loaded multiple times, only keep the last
        //         mods.Remove(modValues.mod);
        //         mods.Add(modValues.mod);

        //         // species
        //         if (modValues.species != null)
        //         {
        //             foreach (Species sp in modValues.species)
        //             {
        //                 if (string.IsNullOrWhiteSpace(sp.blueprintPath)) continue;

        //                 Species originalSpecies = SpeciesByBlueprint(sp.blueprintPath);
        //                 if (originalSpecies != null)
        //                 {
        //                     _V.species.Remove(originalSpecies);
        //                 }
        //                 _V.species.Add(sp);
        //                 sp.Mod = modValues.mod;
        //                 speciesAdded++;

        //                 if (!blueprintToSpecies.ContainsKey(sp.blueprintPath))
        //                     blueprintToSpecies.Add(sp.blueprintPath, sp);
        //             }
        //         }

        //         // TODO support for mod colors
        //     }

        //     loadedModsHash = CreatureCollection.CalculateModListHash(mods);

        //     if (speciesAdded == 0)
        //     {
        //         resultsMessage = resultsMessageSB.ToString();
        //         // nothing changed
        //         return false;
        //     }

        //     // sort new species
        //     OrderSpeciesAndApplyCustomVariants();

        //     // mod food data TODO

        //     _V.LoadAliases();
        //     _V.UpdateSpeciesBlueprintDictionaries();

        //     resultsMessageSB.AppendLine($"The following mods were loaded:\n\n- {string.Join("\n- ", modifiedValues.Select(m => m.mod.title).ToArray())}\n\n"
        //                    + $"Species added: {speciesAdded}");
        //     resultsMessage = resultsMessageSB.ToString();

        //     return true;
        // }

        /// <summary>
        /// Check if all mod files are available and uptodate, and download the ones not available locally.
        /// </summary>
        /// <param name="modValueFileNames"></param>
        // internal (List<string> missingModValueFilesOnlineAvailable, List<string> missingModValueFilesOnlineNotAvailable, List<string> modValueFilesWithAvailableUpdate)
        //     CheckAvailabilityAndUpdateModFiles(List<string> modValueFileNames)
        // {
        //     if (modsManifest == null) throw new ArgumentNullException(nameof(modsManifest));

        //     List<string> missingModValueFilesOnlineAvailable = new List<string>();
        //     List<string> missingModValueFilesOnlineNotAvailable = new List<string>();
        //     List<string> modValueFilesWithAvailableUpdate = new List<string>();

        //     string valuesFolder = FileService.GetJsonPath(FileService.ValuesFolder);

        //     foreach (string mf in modValueFileNames)
        //     {
        //         string modFilePath = Path.Combine(valuesFolder, mf);

        //         modsManifest.modsByFiles.TryGetValue(mf, out var modInfo);

        //         if (!File.Exists(modFilePath))
        //         {
        //             if (modInfo != null
        //                 && modInfo.OnlineAvailable
        //                 && IsValidFormatVersion(modInfo.format))
        //                 missingModValueFilesOnlineAvailable.Add(mf);
        //             else
        //                 missingModValueFilesOnlineNotAvailable.Add(mf);
        //         }
        //         else if (modInfo != null)
        //         {
        //             // check if an update is available
        //             if (modInfo.OnlineAvailable
        //                 && IsValidFormatVersion(modInfo.format)
        //                 && modInfo.Version != null
        //                 && (!TryLoadValuesFile(modFilePath, setModFileName: false, throwExceptionOnFail: false,
        //                     out Values modValues, errorMessage: out _)
        //                     || modValues.Version < modsManifest.modsByFiles[mf].Version))
        //             {
        //                 modValueFilesWithAvailableUpdate.Add(mf);
        //             }
        //         }
        //     }

        //     return (missingModValueFilesOnlineAvailable,
        //             missingModValueFilesOnlineNotAvailable,
        //             modValueFilesWithAvailableUpdate);
        // }

        [OnDeserialized]
        private void ParseVersion(StreamingContext ct)
        {
            if (!Version.TryParse(version, out Version))
                Version = new Version(0, 0);

            Colors = new ArkColors(colorDefinitions, dyeDefinitions);
            Dyes = new ArkColors(dyeDefinitions);

            foreach (var s in species)
                s.InitializeColors(Colors);

            //// for debugging, test if there are duplicates in the species-names
            //var duplicateSpeciesNames = string.Join("\n", species
            //                                   //.GroupBy(s => s.DescriptiveName)
            //                                   .GroupBy(s => s.NameAndMod)
            //                                   .Where(g => g.Count() > 1)
            //                                   .Select(x => x.Key)
            //                                   .ToArray());
            //if (!string.IsNullOrEmpty(duplicateSpeciesNames))
            //    Clipboard.SetText(duplicateSpeciesNames);
        }

        private string SpeciesNameSortFilePath => FileService.GetJsonPath("sortNames.txt");

        public void ResetDefaultSpeciesNameSorting()
        {
            string filePath = SpeciesNameSortFilePath;

            try
            {
                File.WriteAllText(filePath, "^(Aberrant |Tek |R\\-|X\\-)(.*)$@$2$1\n");
                ApplySpeciesOrdering();
            }
            catch
            {
                // ignored
            }
        }

        public void ResetSpeciesNameSorting()
        {
            string filePath = SpeciesNameSortFilePath;
            if (FileService.TryDeleteFile(filePath))
                ApplySpeciesOrdering();
        }

        public void OpenSpeciesNameSortingFile()
        {
            string filePath = SpeciesNameSortFilePath;
            if (!File.Exists(filePath))
                File.WriteAllText(filePath, string.Empty);
            if (File.Exists(filePath))
                Process.Start(filePath);
        }

        private void ApplySpeciesOrdering()
        {
            string filePath = SpeciesNameSortFilePath;

            if (File.Exists(filePath))
            {
                foreach (Species.Species s in _V.species)
                    s.SortName = string.Empty;

                string[] lines = File.ReadAllLines(filePath);
                foreach (string l in lines)
                {
                    if (l.IndexOf("@", StringComparison.Ordinal) <= 0 ||
                        l.IndexOf("@", StringComparison.Ordinal) + 1 >= l.Length)
                        continue;
                    string matchName = l.Substring(0, l.IndexOf("@", StringComparison.Ordinal)).Trim();
                    string replaceName = l.Substring(l.IndexOf("@", StringComparison.Ordinal) + 1).Trim();

                    Regex r = new Regex(matchName);

                    List<Species.Species> matchedSpecies =
                        _V.species.Where(s => string.IsNullOrEmpty(s.SortName) && r.IsMatch(s.name)).ToList();

                    foreach (Species.Species s in matchedSpecies)
                    {
                        s.SortName = r.Replace(s.name, replaceName);
                    }
                }

                // set each sortName of species without manual sortName to its speciesName
                foreach (Species.Species s in _V.species)
                {
                    if (string.IsNullOrEmpty(s.SortName))
                        s.SortName = s.DescriptiveNameAndMod;
                }
            }
            else
            {
                foreach (Species.Species s in _V.species)
                {
                    s.SortName = s.DescriptiveNameAndMod;
                }
            }

            _V.species = _V.species.OrderBy(s => s.SortName).ToList();
        }

        private void OrderSpeciesAndApplyCustomVariants()
        {
            ApplySpeciesOrdering();
            _V.speciesNames = _V.species.Select(s => s.name).ToList();

            // apply custom species variants
            var customSpeciesVariantsFilePath = FileService.GetJsonPath(FileService.CustomSpeciesVariants);

            if (File.Exists(customSpeciesVariantsFilePath)
                && FileService.LoadJsonFile(customSpeciesVariantsFilePath,
                    out Dictionary<string, string[]> customSpeciesVariants, out var error))
            {
                if (customSpeciesVariants.Any())
                {
                    foreach (Species.Species sp in _V.species)
                    {
                        if (customSpeciesVariants.TryGetValue(sp.blueprintPath, out var variants))
                        {
                            var spVars = (sp.variants?.ToList() ?? new List<string>());
                            spVars.AddRange(variants);
                            sp.variants = spVars.Any() ? spVars.Distinct().ToArray() : null;
                            sp.InitializeNames();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Applies the serverMultipliers and creates precalculated species-stats values
        /// </summary>
        /// <param name="cc"></param>
        /// <param name="eventMultipliers"></param>
        /// <param name="applyStatMultipliers"></param>
        // public void ApplyMultipliers(CreatureCollection cc, bool eventMultipliers = false, bool applyStatMultipliers = true)
        // {
        //     currentServerMultipliers = (eventMultipliers ? cc.serverMultipliersEvents : cc.serverMultipliers)?.Copy(false);
        //     if (currentServerMultipliers == null) currentServerMultipliers = V.serverMultipliersPresets.GetPreset(ServerMultipliersPresets.Official);
        //     if (currentServerMultipliers == null)
        //     {
        //         throw new FileNotFoundException("No default server multiplier values found.\nIt's recommend to redownload ARK Smart Breeding.");
        //     }

        //     ServerMultipliers singlePlayerServerMultipliers = null;

        //     if (cc.singlePlayerSettings)
        //     {
        //         // The singleplayer multipliers are saved as a regular multiplierpreset, but they work differently
        //         // in the way they are multiplied on existing multipliers and won't work on their own.
        //         // The preset name "singleplayer" should only be used for this purpose.
        //         singlePlayerServerMultipliers = serverMultipliersPresets.GetPreset(ServerMultipliersPresets.Singleplayer);
        //         if (singlePlayerServerMultipliers == null)
        //             throw new FileNotFoundException("No server multiplier values for singleplayer settings found.\nIt's recommend to redownload ARK Smart Breeding.");
        //     }

        //     if (singlePlayerServerMultipliers != null)
        //     {
        //         currentServerMultipliers.MatingIntervalMultiplier *= singlePlayerServerMultipliers.MatingIntervalMultiplier;
        //         currentServerMultipliers.EggHatchSpeedMultiplier *= singlePlayerServerMultipliers.EggHatchSpeedMultiplier;
        //         currentServerMultipliers.BabyMatureSpeedMultiplier *= singlePlayerServerMultipliers.BabyMatureSpeedMultiplier;
        //         currentServerMultipliers.BabyCuddleIntervalMultiplier *= singlePlayerServerMultipliers.BabyCuddleIntervalMultiplier;
        //         currentServerMultipliers.TamingSpeedMultiplier *= singlePlayerServerMultipliers.TamingSpeedMultiplier;
        //     }

        //     currentServerMultipliers.FixZeroValues();
        //     double[] defaultMultipliers = new double[] { 1, 1, 1, 1 }; // used if serverMultipliers don't specify non-default values

        //     foreach (Species sp in species)
        //     {
        //         if (applyStatMultipliers)
        //         {
        //             bool customOverrideExists = cc.CustomSpeciesStats?.ContainsKey(sp.blueprintPath) ?? false;
        //             double?[][] customFullStatsRaw = customOverrideExists ? cc.CustomSpeciesStats[sp.blueprintPath] : null;
        //             bool useSpeedLevelup = currentServerMultipliers.AllowFlyerSpeedLeveling || !sp.isFlyer;

        //             // stat-multiplier
        //             for (int s = 0; s < STATS_COUNT; s++)
        //             {
        //                 double[] statMultipliers = cc.serverMultipliers?.statMultipliers?[s] ?? defaultMultipliers;

        //                 bool customOverrideForThisStatExists = customOverrideExists && customFullStatsRaw[s] != null;

        //                 sp.stats[s].BaseValue = GetRawStatValue(s, 0, customOverrideForThisStatExists);

        //                 // don't apply the multiplier if AddWhenTamed is negative (e.g. Giganotosaurus, Griffin)
        //                 double addWhenTamed = GetRawStatValue(s, 3, customOverrideForThisStatExists);
        //                 sp.stats[s].AddWhenTamed = addWhenTamed * (addWhenTamed > 0 ? statMultipliers[0] : 1);

        //                 // don't apply the multiplier if MultAffinity is negative (e.g. Aberration variants)
        //                 double multAffinity = GetRawStatValue(s, 4, customOverrideForThisStatExists);
        //                 sp.stats[s].MultAffinity = multAffinity * (multAffinity > 0 ? statMultipliers[1] : 1);

        //                 if (useSpeedLevelup || s != (int)StatNames.SpeedMultiplier)
        //                 {
        //                     sp.stats[s].IncPerTamedLevel = GetRawStatValue(s, 2, customOverrideForThisStatExists) * statMultipliers[2];
        //                 }
        //                 else
        //                 {
        //                     sp.stats[s].IncPerTamedLevel = 0;
        //                 }

        //                 sp.stats[s].IncPerWildLevel = GetRawStatValue(s, 1, customOverrideForThisStatExists) * statMultipliers[3];

        //                 // set troodonism values
        //                 if (sp.altStats?[s] != null && sp.stats[s].BaseValue != 0)
        //                 {
        //                     sp.altStats[s].BaseValue = sp.altBaseStatsRaw[s];

        //                     // alt / troodonism values depend on the base value
        //                     var altFactor = sp.altStats[s].BaseValue / sp.stats[s].BaseValue;

        //                     sp.altStats[s].AddWhenTamed = altFactor * sp.stats[s].AddWhenTamed;
        //                     sp.altStats[s].MultAffinity = altFactor * sp.stats[s].MultAffinity;
        //                     sp.altStats[s].IncPerTamedLevel = altFactor * sp.stats[s].IncPerTamedLevel;
        //                     sp.altStats[s].IncPerWildLevel = altFactor * sp.stats[s].IncPerWildLevel;
        //                 }

        //                 if (singlePlayerServerMultipliers?.statMultipliers?[s] == null)
        //                     continue;

        //                 // don't apply the multiplier if AddWhenTamed is negative (e.g. Giganotosaurus, Griffin)
        //                 sp.stats[s].AddWhenTamed *= sp.stats[s].AddWhenTamed > 0 ? singlePlayerServerMultipliers.statMultipliers[s][0] : 1;
        //                 // don't apply the multiplier if MultAffinity is negative (e.g. Aberration variants)
        //                 sp.stats[s].MultAffinity *= sp.stats[s].MultAffinity > 0 ? singlePlayerServerMultipliers.statMultipliers[s][1] : 1;
        //                 sp.stats[s].IncPerTamedLevel *= singlePlayerServerMultipliers.statMultipliers[s][2];
        //                 sp.stats[s].IncPerWildLevel *= singlePlayerServerMultipliers.statMultipliers[s][3];

        //                 // troodonism values
        //                 if (sp.altStats?[s] != null)
        //                 {
        //                     sp.altStats[s].AddWhenTamed *= sp.altStats[s].AddWhenTamed > 0
        //                         ? singlePlayerServerMultipliers.statMultipliers[s][0]
        //                         : 1;
        //                     sp.altStats[s].MultAffinity *= sp.altStats[s].MultAffinity > 0
        //                         ? singlePlayerServerMultipliers.statMultipliers[s][1]
        //                         : 1;
        //                     sp.altStats[s].IncPerTamedLevel *= singlePlayerServerMultipliers.statMultipliers[s][2];
        //                     sp.altStats[s].IncPerWildLevel *= singlePlayerServerMultipliers.statMultipliers[s][3];
        //                 }

        //                 double GetRawStatValue(int statIndex, int statValueTypeIndex, bool customOverride)
        //                 {
        //                     return customOverride && customFullStatsRaw[statIndex][statValueTypeIndex].HasValue ? customFullStatsRaw[statIndex][statValueTypeIndex].Value : sp.fullStatsRaw[statIndex][statValueTypeIndex];
        //                 }
        //             }

        //             // imprinting multiplier override
        //             sp.SetCustomImprintingMultipliers(customOverrideExists && cc.CustomSpeciesStats[sp.blueprintPath].Length > Values.STATS_COUNT ? cc.CustomSpeciesStats[sp.blueprintPath][Values.STATS_COUNT] : null);
        //         }

        //         // breeding multiplier
        //         if (sp.breeding == null)
        //             continue;
        //         if (currentServerMultipliers.EggHatchSpeedMultiplier > 0)
        //         {
        //             sp.breeding.gestationTimeAdjusted = sp.breeding.gestationTime / currentServerMultipliers.EggHatchSpeedMultiplier;
        //             sp.breeding.incubationTimeAdjusted = sp.breeding.incubationTime / currentServerMultipliers.EggHatchSpeedMultiplier;
        //         }
        //         if (currentServerMultipliers.MatingSpeedMultiplier > 0)
        //             sp.breeding.matingTimeAdjusted = sp.breeding.matingTime / currentServerMultipliers.MatingSpeedMultiplier;
        //         if (currentServerMultipliers.BabyMatureSpeedMultiplier > 0)
        //             sp.breeding.maturationTimeAdjusted = sp.breeding.maturationTime / currentServerMultipliers.BabyMatureSpeedMultiplier;

        //         sp.breeding.matingCooldownMinAdjusted = sp.breeding.matingCooldownMin * currentServerMultipliers.MatingIntervalMultiplier;
        //         sp.breeding.matingCooldownMaxAdjusted = sp.breeding.matingCooldownMax * currentServerMultipliers.MatingIntervalMultiplier;
        //     }
        // }

        /// <summary>
        /// Loads the species aliases from a file.
        /// </summary>
        private bool LoadAliases()
        {
            aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            speciesWithAliasesList = new List<string>(speciesNames);

            try
            {
                using (StreamReader reader = FileService.GetJsonFileReader(FileService.AliasesJson))
                {
                    JObject aliasesNode = (JObject)JToken.ReadFrom(new JsonTextReader(reader));
                    foreach (KeyValuePair<string, JToken> pair in aliasesNode)
                    {
                        string alias = pair.Key;
                        string speciesName = pair.Value.Value<string>();
                        if (speciesNames.Contains(alias, StringComparer.OrdinalIgnoreCase)
                                || !speciesNames.Contains(speciesName, StringComparer.OrdinalIgnoreCase)
                                || aliases.ContainsKey(alias))
                            continue;
                        aliases.Add(alias, speciesName);
                        speciesWithAliasesList.Add(alias);
                    }
                    speciesWithAliasesList.Sort();
                    return true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                // TODO create log-file for this?
                //MessageBox.Show($"Couldn't load {FileService.AliasesJson}\nThe program will continue without it.\n" +
                //        $"Error message:\n\n{e.Message}",
                //        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Creates dictionaries to select species
        /// </summary>
        private void UpdateSpeciesBlueprintDictionaries()
        {
            blueprintToSpecies = new Dictionary<string, Species.Species>();
            nameToSpecies = new Dictionary<string, Species.Species>(StringComparer.OrdinalIgnoreCase);
            classNameToSpecies = new Dictionary<string, Species.Species>(StringComparer.OrdinalIgnoreCase);

            Regex rClassName = new Regex(@"(?<=\.)[^\/\.]+$");

            foreach (Species.Species s in species)
            {
                if (!string.IsNullOrEmpty(s.blueprintPath))
                {
                    if (!blueprintToSpecies.ContainsKey(s.blueprintPath))
                        blueprintToSpecies.Add(s.blueprintPath, s);

                    string name = s.name;
                    if (nameToSpecies.TryGetValue(name, out var existingSpecies))
                    {
                        if (
                            (!existingSpecies.IsDomesticable && s.IsDomesticable) // prefer species that are domesticable
                            || (existingSpecies.Mod == null && s.Mod != null) // prefer species from mods with the same name
                            || ((existingSpecies.variants?.Length ?? 0) > (s.variants?.Length ?? 0)) // prefer species that are not variants
                        )
                            nameToSpecies[name] = s;
                    }
                    else
                        nameToSpecies.Add(name, s);

                    Match classNameMatch = rClassName.Match(s.blueprintPath);
                    if (classNameMatch.Success)
                    {
                        string className = classNameMatch.Value + "_C";
                        if (classNameToSpecies.ContainsKey(className))
                            classNameToSpecies[className] = s;
                        else
                            classNameToSpecies.Add(className, s);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if the passed name is an available species name or an alias, then returns the species
        /// </summary>
        /// <param name="alias"></param>
        /// <returns>Available species name or empty, if not available.</returns>
        public string SpeciesName(string alias)
        {
            if (speciesNames.Contains(alias))
                return alias;
            return aliases.TryGetValue(alias, out var speciesName) ? speciesName : string.Empty;
        }

        /// <summary>
        /// Checks species names and loaded aliases for a match and sets the out parameter.
        /// Especially when mods are used, this is not guaranteed to result in the correct species.
        /// </summary>
        /// <param name="speciesName"></param>
        /// <param name="recognizedSpecies"></param>
        /// <returns>True on success</returns>
        public bool TryGetSpeciesByName(string speciesName, out Species.Species recognizedSpecies)
        {
            recognizedSpecies = null;
            if (string.IsNullOrEmpty(speciesName)) return false;

            if (aliases.TryGetValue(speciesName, out var realSpeciesName))
                speciesName = realSpeciesName;
            if (nameToSpecies.TryGetValue(speciesName, out var s))
            {
                recognizedSpecies = s;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks species for a matching className.
        /// Especially when mods are used, this is not garantueed to result in the correct species.
        /// </summary>
        /// <param name="speciesClassName"></param>
        /// <param name="recognizedSpecies"></param>
        /// <returns>True on success</returns>
        public bool TryGetSpeciesByClassName(string speciesClassName, out Species.Species recognizedSpecies)
        {
            recognizedSpecies = null;
            if (string.IsNullOrEmpty(speciesClassName)) return false;

            if (classNameToSpecies.TryGetValue(speciesClassName, out var s))
            {
                recognizedSpecies = s;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the according species to the passed blueprintpath or null if unknown.
        /// </summary>
        /// <param name="blueprintPath"></param>
        /// <returns></returns>
        public Species.Species SpeciesByBlueprint(string blueprintPath)
        {
            if (string.IsNullOrEmpty(blueprintPath)) return null;
            if (blueprintRemapping != null && blueprintRemapping.TryGetValue(blueprintPath, out var realBlueprintPath))
            {
                blueprintPath = realBlueprintPath;
            }
            return blueprintToSpecies.TryGetValue(blueprintPath, out var s) ? s : null;
        }

        /// <summary>
        /// Sets the ModsManifest. If the value is null, a new default object will be created.
        /// </summary>
        /// <param name="mm"></param>
        // internal void SetModsManifest(ModsManifest mm)
        // {
        //     modsManifest = mm ?? new ModsManifest();
        // }

        private void LoadIgnoreSpeciesClassesFile()
        {
            ignoreSpeciesClassesOnImport = new List<string>();
            try
            {
                using (StreamReader reader = FileService.GetJsonFileReader(FileService.IgnoreSpeciesClasses))
                {
                    JArray aliasesNode = (JArray)JToken.ReadFrom(new JsonTextReader(reader));
                    foreach (string speciesClass in aliasesNode)
                    {
                        if (!ignoreSpeciesClassesOnImport.Contains(speciesClass))
                            ignoreSpeciesClassesOnImport.Add(speciesClass);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                // TODO create log file for this?
                //MessageBox.Show($"Couldn't load {FileService.IgnoreSpeciesClasses}\nThe program will continue without it.\n" +
                //        $"Error message:\n\n{e.Message}",
                //        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal List<string> IgnoreSpeciesClassesOnImport
        {
            get
            {
                if (ignoreSpeciesClassesOnImport == null)
                    LoadIgnoreSpeciesClassesFile();
                return ignoreSpeciesClassesOnImport;
            }
        }

        /// <summary>
        /// Checks if a speciesBlueprintpath belongs to a species that should be ignored on import.
        /// </summary>
        /// <param name="speciesBlueprintPath"></param>
        /// <returns></returns>
        internal bool IgnoreSpeciesBlueprint(string speciesBlueprintPath)
        {
            if (string.IsNullOrEmpty(speciesBlueprintPath)) return true;

            // check if species should be ignored (e.g. if it's a raft)
            var m = Regex.Match(speciesBlueprintPath, @"\/([^\/\.]+)\.");
            if (!m.Success) return false;

            string speciesClassString = m.Groups[1].Value;
            if (!speciesClassString.EndsWith("_C")) speciesClassString += "_C";
            return IgnoreSpeciesClassesOnImport.Contains(speciesClassString);
        }

        /// <summary>
        /// Returns the taming food data for a species.
        /// Returns null if no data is found.
        /// </summary>
        internal TamingFood GetTamingFood(Species.Species species, string foodName)
        {
            if (species?.taming?.specialFoodValues != null
                && species.taming.specialFoodValues.TryGetValue(foodName, out var food))
                return food;

            if (defaultFoodData != null
                && defaultFoodData.TryGetValue(foodName, out food))
                return food;
            return null;
        }
    }
}
