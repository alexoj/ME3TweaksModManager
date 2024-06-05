using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.objects.mod;

namespace ME3TweaksModManager.modmanager.objects.alternates
{
    /// <summary>
    /// Container class for a conditional dlc
    /// </summary>
    public class ConditionalDLC
    {
        /// <summary>
        /// DLC folder name
        /// </summary>
        public PlusMinusKey DLCName { get; set; }

        public List<PlusMinusKey> OptionKeyDependencies { get; set; } = new(0);

        public ConditionalDLC(Mod mod, string input, bool canBePlusMinus = false)
        {
            var dlcName = input;
            DLCName = canBePlusMinus ? new PlusMinusKey(dlcName) : new PlusMinusKey(null, input);
            if (mod.ModDescTargetVersion >= 9.0)
            {
                var openParenPos = input.IndexOf('(');
                var closeParenPos = input.IndexOf(')');
                if (openParenPos > 0 && closeParenPos == -1)
                {
                    throw new Exception($"Conditional DLC {input} contains opening parenthesis but no closing parenthesis.");
                }

                if (openParenPos > 0)
                {
                    DLCName.Key = DLCName.Key.Substring(0, openParenPos);
                    var conditionalDLCStruct = input.Substring(openParenPos, closeParenPos + 1);
                    var structKeys = StringStructParser.GetCommaSplitList(conditionalDLCStruct);
                    foreach (var key in structKeys)
                    {
                        OptionKeyDependencies.Add(new PlusMinusKey(key));
                    }
                }
            }
        }
    }
}
