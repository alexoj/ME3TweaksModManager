using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.objects.mod.merge.v1
{
    public class ClassUpdateAsset1
    {
        /// <summary>
        /// The Instanced Full Path of the class. This is so you can add classes to EntryMenu.
        /// </summary>
        [JsonProperty("classinstancedfullpath")]
        public string ClassInstancedFullPath { get; set; }

        /// <summary>
        /// The name of the asset that contains the class text in the M3M
        /// </summary>
        [JsonProperty("classfilename")]
        public string ClassTextFilename { get; set; }

        
    }
}
