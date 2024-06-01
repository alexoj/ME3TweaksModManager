using System.Threading;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using ME3TweaksCore.GameFilesystem;

namespace ME3TweaksModManager.modmanager.objects.mod
{
    // Experimental, potentially unreliable features
    public partial class Mod
    {

        /// <summary>
        /// Attempts to determine a mod's mount priority. This will return 0 on mods without DLC folders and will not be reliable for multi-DLC mods.
        /// </summary>
        /// <returns></returns>
        public int EXP_GetModMountPriority()
        {
            if (IsInArchive)
                throw new Exception("Cannot get a mod's mount priority while it is in an archive! This is a bug.");

            var custDLC = GetJob(ModJob.JobHeader.CUSTOMDLC);
            if (custDLC == null || custDLC.CustomDLCFolderMapping.Count == 0)
            {
                // No DLC folder
                return 0;
            }

            // We will take the mod's LOWEST mount priority
            // A multi-dlc mod may have additional dlc folders as patches or extra content. It likely will have a higher
            // mount. So we will take the lowest one

            int lowestMount = int.MaxValue;
            foreach (var fm in custDLC.CustomDLCFolderMapping)
            {
                var dlcPath = Path.Combine(ModPath, fm.Key);

                // Should be in a try catch
                try
                {
                    var mount = MELoadedDLC.GetMountPriority(dlcPath, Game);
                    lowestMount = Math.Min(mount, lowestMount);
                }
                catch (Exception ex)
                {
                    M3Log.Error($@"Unable to get mount priority value for mod {ModName}'s DLC folder {dlcPath}: {ex.Message}. We will skip this mount.");
                }

            }

            // If no mount value was set, return the default value of 0
            if (lowestMount == int.MaxValue)
                lowestMount = 0;

            return lowestMount;
        }



        public bool IsInstalledToTarget { get; set; }
        
        /// <summary>
        /// Attempts to determine if a mod is installed to the given target. This is not reliable.
        /// </summary>
        /// <param name="target"></param>
        public void DetermineIfInstalled(GameTarget target, CaseInsensitiveDictionary<MetaCMM> metaCMMs)
        {
            foreach (var metaCMM in metaCMMs)
            {
                if (metaCMM.Value.ModdescSourcePath == M3LoadedMods.GetRelativeModdescPath(this))
                {
                    IsInstalledToTarget = true;
                    return;
                }
            }

            IsInstalledToTarget = false;
        }
    }
}
