using System.Collections.Concurrent;
using ME3TweaksModManager.modmanager.importer;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.nexusmodsintegration
{
    public class DownloadManagerKey
    {
        protected bool Equals(DownloadManagerKey other)
        {
            return Domain == other.Domain && FileID == other.FileID;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((DownloadManagerKey)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Domain, FileID);
        }

        public string Domain { get; set; }
        public int FileID { get; set; }
    }

    /// <summary>
    /// Manager class for NexusMod downloads
    /// </summary>
    public static class DownloadManager
    {
        /// <summary>
        /// The list of downloads. They may not all be actively downloading, but in a queued state.
        /// </summary>
        public static ConcurrentDictionary<string, ModDownload> Downloads = new();


        /// <summary>
        /// Invoked when a mod has initialized
        /// </summary>
        public static event EventHandler<EventArgs> OnModInitialized;

        public static void QueueNXMDownload(string nxmLink)
        {
            M3Log.Information($@"Queueing nxmlink {nxmLink}");
            var dl = new NexusModDownload(nxmLink);
            dl.DownloadStateChanged += DownloadStateChanged;
            //dl.OnInitialized += ModInitialized;
            //dl.OnModDownloaded += ModDownloaded;
            //dl.OnModDownloadError += DownloadError;
            dl.Initialize();
        }

        private static void ModDownloaded(object sender, DataEventArgs e)
        {
            if (sender is ModDownload md)
            {
                md.OnModDownloaded -= ModDownloaded;
                //Application.Current.Dispatcher.Invoke(() =>
                //{
                //    if (cancellationTokenSource.IsCancellationRequested)
                //    {
                //        // Canceled
                //        OnClosing(DataEventArgs.Empty);
                //    }
                //    else
                //    {
                //        OnClosing(new DataEventArgs(new List<ModDownload>(new[] { md }))); //maybe someday i'll support download queue or something.
                //    }
                //});
            }
        }

        private static void DownloadStateChanged(object sender, EventArgs e)
        {
            if (sender is ModDownload item)
            {
                M3Log.Information($@"ModDownload {item} state changed: {item.DownloadState}");
                if (item.DownloadState == EModDownloadState.QUEUED)
                {
                    // Download has initialized and is queued for download.
                    // Add to list of downloads
                    Downloads.TryAdd(item.CreateDownloadKey(), item);
                    OnModInitialized?.Invoke(item, EventArgs.Empty);
                }

                // Attempt to start download, as states have changed.
                TryStartDownload();

                if (item.DownloadState == EModDownloadState.DOWNLOADCOMPLETE && item.AutoImport)
                {
                    ModArchiveImport mai = new ModArchiveImport()
                    {
                        AutomatedMode = true,
                        ArchiveStream = item.DownloadedStream,
                        GetPanelResult = () => new PanelResult(), // TEMPORARY DO NOT RELY ON THIS
                        ArchiveFilePath = "Test.7z",
                    };
                    if (item is NexusModDownload nmd)
                    {
                        mai.SourceNXMLink = nmd.ProtocolLink;
                    }
                    mai.ImportStateChanged += OnImportStateChange;

                    item.ImportFlow = mai;

                    mai.BeginScan();
                }
            }
        }

        private static void OnImportStateChange(object sender, EventArgs e)
        {
            if (sender is ModArchiveImport mai)
            {
                var matchingObj = Downloads.Values.FirstOrDefault(x => x.ImportFlow == mai);
                if (matchingObj == null)
                    return; 

                switch (mai.CurrentState)
                {
                    case EModArchiveImportState.FAILED:
                        matchingObj.Status = "Import failed";
                        break;
                    case EModArchiveImportState.SCANNING:
                        matchingObj.Status = "Scanning";
                        break;
                    case EModArchiveImportState.SCANCOMPLETED:
                        matchingObj.Status = "Import queued";
                        break;
                    case EModArchiveImportState.IMPORTING:
                        matchingObj.Status = "Importing mods";
                        break;
                    case EModArchiveImportState.COMPLETE:
                        matchingObj.Status = "Import complete";
                        break;
                }
            }
        }

        private static void TryStartDownload()
        {
            if (Downloads.Count == 0 || Downloads.All(x => x.Value.DownloadState != EModDownloadState.QUEUED))
                return; // Nothing to do

            if (!NexusModsUtilities.UserInfo.IsPremium)
            {
                // Ensure only one download a time - nexus doesn't support concurrent downloads... I think?
                var currentDownloadCount = Downloads.Any(x => x.Value.DownloadState == EModDownloadState.DOWNLOADING);
                if (currentDownloadCount)
                {
                    // Cannot download until previous one is complete
                    return;
                }

                Downloads.First(x => x.Value.DownloadState == EModDownloadState.QUEUED).Value.StartDownload(default, true);
            }
            else
            {
                foreach (var download in Downloads.Where(x => x.Value.DownloadState == EModDownloadState.QUEUED))
                {
                    // Todo improve this
                    download.Value.StartDownload(default, true);
                }

            }
        }

        private static void DownloadError(object sender, string e)
        {
            //Application.Current.Dispatcher.Invoke(() =>
            //{
            //    M3L.ShowDialog(window, e, M3L.GetString(M3L.string_downloadError), MessageBoxButton.OK, MessageBoxImage.Error);
            //    OnClosing(DataEventArgs.Empty);
            //});
        }
    }
}
