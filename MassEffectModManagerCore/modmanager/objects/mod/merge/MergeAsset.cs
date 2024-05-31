using System.IO;
using System.Text;
using System.Windows.Shapes;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Helpers;
using Newtonsoft.Json;

namespace ME3TweaksModManager.modmanager.objects.mod.merge
{
    /// <summary>
    /// Asset file packaged in a merge mod
    /// </summary>
    public class MergeAsset
    {
        /// <summary>
        /// Filename of the asset. Only used during serialization and for logging errors
        /// </summary>
        [JsonProperty(@"filename")]
        public string FileName { get; set; }

        /// <summary>
        /// Size of the asset (uncompressed)
        /// </summary>
        [JsonProperty(@"filesize")]
        public int FileSize { get; set; }

        /// <summary>
        /// If the asset is compressed
        /// </summary>
        [JsonProperty(@"compressed")]
        public bool IsCompressed { get; set; }

        /// <summary>
        /// Size of the asset (compressed) in the file
        /// </summary>
        [JsonProperty(@"compressedsize")]
        public int? CompressedSize { get; set; }

        /// <summary>
        /// Asset binary data. This is only loaded when needed - mods loaded from archive will have this populated, mods loaded from disk will not.
        /// </summary>
        [JsonIgnore]
        public byte[] AssetBinary;

        /// <summary>
        /// Where the data for this asset begins in the stream (post magic). Used when loading from disk on demand. Mods loaded from archive will load it when the file is parsed
        /// </summary>
        [JsonIgnore]
        public int FileOffset;

        /// <summary>
        /// Reads the asset binary data into the <see cref="AssetBinary"/> byte array. Seeks and reads from the specified stream.
        /// </summary>
        /// <param name="mergeFileStream">The stream to read from.</param>
        public void ReadAssetBinary(Stream mergeFileStream)
        {
            if (FileOffset != 0)
                mergeFileStream.Seek(FileOffset, SeekOrigin.Begin);
            if (IsCompressed)
            {
                var compressed = mergeFileStream.ReadToBuffer(CompressedSize.Value);
                AssetBinary = new byte[FileSize];
                AssetBinary = LZMA.Decompress(compressed, (uint)FileSize);
            }
            else
            {
                // Mod Manager 8.x only supported uncompressed assets
                AssetBinary = mergeFileStream.ReadToBuffer(FileSize);
            }
        }

        /// <summary>
        /// Returns the AssetBinary as a string.
        /// </summary>
        /// <returns></returns>
        public string AsString()
        {
            if (AssetBinary == null)
                throw new Exception(@"AssetBinary was null in this MergeAsset! The m3m was not loaded. This is a bug.");
            // This is how File.ReadAllText() works
            using StreamReader sr = new StreamReader(new MemoryStream(AssetBinary), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return sr.ReadToEnd();
        }

        public MergeAsset(){}

        public MergeAsset(string assetName, bool compressAsset)
        {
            FileName = assetName;
            IsCompressed = compressAsset;
        }
    }
}
