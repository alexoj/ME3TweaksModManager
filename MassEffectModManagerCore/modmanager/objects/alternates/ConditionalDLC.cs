using LegendaryExplorerCore.Misc;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Objects;
using ME3TweaksModManager.modmanager.objects.mod;

namespace ME3TweaksModManager.modmanager.objects.alternates
{
    /// <summary>
    /// Container class for a conditional dlc
    /// </summary>
    public class ConditionalDLC
    {
        private const string REQKEY_DLCOPTIONKEY = @"optionkey";
        private const string REQKEY_MINVERSION = @"minversion";
        private const string REQKEY_MAXVERSION = @"maxversion"; // I highly doubt this will be used, but maybe some new mod will make people unhappy and they will try to use old version. DLCRequirements does not support this as it doesn't really make sense to only allow install against older version

        /// <summary>
        /// DLC folder name
        /// </summary>
        public PlusMinusKey DLCName { get; set; }

        public List<PlusMinusKey> OptionKeyDependencies { get; set; } = new(0);

        public Version MinVersion { get; set; }
        public Version MaxVersion { get; set; }

        public ConditionalDLC(Mod mod, string input, bool canBePlusMinus = false)
        {
            var dlcName = input;
            DLCName = canBePlusMinus ? new PlusMinusKey(dlcName) : new PlusMinusKey(null, input);
            if (mod.ModDescTargetVersion >= 9.0)
            {
                if (input.Contains('(') || input.Contains(')'))
                    throw new Exception("ConditionalDLC cannot contain ( or ) due to parser issues that would potentially break backwards compatibility if fixed. Use [ ] instead for conditions.");


                var openParenPos = input.IndexOf('[');
                var closeParenPos = input.IndexOf(']'); // Last Index of?
                if (openParenPos > 0 && closeParenPos == -1)
                {
                    throw new Exception($"ConditionalDLC {input} contains opening bracket but no closing bracket.");
                }

                if (openParenPos > 0)
                {
                    DLCName.Key = DLCName.Key.Substring(0, openParenPos);
                    var conditionalDLCStruct = input.Substring(openParenPos, closeParenPos - openParenPos + 1);
                    var structKeys = StringStructParser.GetSplitMultiValues(conditionalDLCStruct);
                    foreach (var param in structKeys)
                    {
                        switch (param.Key)
                        {
                            case ConditionalDLC.REQKEY_DLCOPTIONKEY when mod.ModDescTargetVersion >= 9.0: // This is gated above, but this is for if we add more to the switch statement.
                                OptionKeyDependencies.AddRange(param.Value.Select(x => new PlusMinusKey(x)));
                                break;
                            case ConditionalDLC.REQKEY_MINVERSION when mod.ModDescTargetVersion >= 9.0: // This is gated above, but this is for if we add more to the switch statement.
                                OptionKeyDependencies.AddRange(param.Value.Select(x => new PlusMinusKey(x)));
                                break;
                            case ConditionalDLC.REQKEY_MAXVERSION when mod.ModDescTargetVersion >= 9.0: // This is gated above, but this is for if we add more to the switch statement.
                                OptionKeyDependencies.AddRange(param.Value.Select(x => new PlusMinusKey(x)));
                                break;
                        }
                    }
                }
            }
        }


        /// <summary>
        /// ToString() for this. This is what gets serialized into ModDesc!
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (!HasConditions())
                return DLCName.ToString();

            CaseInsensitiveDictionary<List<string>> keyMap = new CaseInsensitiveDictionary<List<string>>();
            if (MinVersion != null)
                keyMap[REQKEY_MINVERSION] = [MinVersion.ToString()];
            if (MaxVersion != null)
                keyMap[REQKEY_MAXVERSION] = [MaxVersion.ToString()];
            if (OptionKeyDependencies != null)
                keyMap[REQKEY_DLCOPTIONKEY] = OptionKeyDependencies.Select(x => x.ToString()).ToList();

            return $@"{DLCName}{StringStructParser.BuildCommaSeparatedSplitMultiValueList(keyMap, '[', ']')}";
        }

        private bool HasConditions()
        {
            return MinVersion != null || MaxVersion != null || OptionKeyDependencies.Any();
        }
    }
}
