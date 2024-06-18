using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.NativeMods;
using ME3TweaksCore.Services.ThirdPartyModIdentification;
using ME3TweaksCoreWPF.UI;
using ME3TweaksModManager.extensions;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.memoryanalyzer;
using ME3TweaksModManager.modmanager.objects;
using ME3TweaksModManager.modmanager.objects.batch;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.texture;
using Microsoft.Win32;

namespace ME3TweaksModManager.modmanager.windows
{
    /// <summary>
    /// Interaction logic for BatchModQueueEditor.xaml
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class BatchModQueueEditor : Window
    {
        // Tab constants
        private const int TAB_CONTENTMOD = 0;
        private const int TAB_ASIMOD = 1;
        private const int TAB_TEXTUREMOD = 2;

        public string NoModSelectedText { get; } = M3L.GetString(M3L.string_selectAModOnTheLeftToViewItsDescription);
        public ObservableCollectionExtended<Mod> VisibleFilteredMods { get; } = new ObservableCollectionExtended<Mod>();
        public ObservableCollectionExtended<ASIMod> VisibleFilteredASIMods { get; } = new ObservableCollectionExtended<ASIMod>();
        public ObservableCollectionExtended<MEMMod> VisibleFilteredMEMMods { get; } = new ObservableCollectionExtended<MEMMod>();

        /// <summary>
        /// Contains both ASI (BatchASIMod) and Content mods (BatchMod)
        /// </summary>
        public ObservableCollectionExtended<object> ModsInGroup { get; } = new ObservableCollectionExtended<object>();

        public MEGameSelector[] Games { get; init; }

        public string AvailableModText { get; set; }

        public string GroupName { get; set; }
        public string GroupDescription { get; set; }
        public string InitialFileName { get; set; }

        /// <summary>
        /// Then newly saved path, for showing in the calling window's UI
        /// </summary>
        public string SavedPath;

        public BatchModQueueEditor(Window owner = null, BatchLibraryInstallQueue queueToEdit = null)
        {
            M3MemoryAnalyzer.AddTrackedMemoryItem(@"Batch Mod Queue Editor", this);
            Owner = owner;
            DataContext = this;
            LoadCommands();
            Games = MEGameSelector.GetGameSelectorsIncludingLauncher().ToArray();

            InitializeComponent();
            this.ApplyDarkNetWindowTheme();

            if (queueToEdit != null)
            {
                SelectedGame = queueToEdit.Game;
                GroupName = queueToEdit.ModName;
                InitialFileName = queueToEdit.BackingFilename;
                GroupDescription = queueToEdit.QueueDescription;
                ModsInGroup.ReplaceAll(queueToEdit.ModsToInstall);
                ModsInGroup.AddRange(queueToEdit.ASIModsToInstall);
                ModsInGroup.AddRange(queueToEdit.TextureModsToInstall);
                VisibleFilteredMods.RemoveRange(queueToEdit.ModsToInstall.Select(x => x.Mod));
                VisibleFilteredASIMods.RemoveRange(queueToEdit.ASIModsToInstall.Select(x => x.AssociatedMod?.OwningMod));
                VisibleFilteredMEMMods.RemoveRange(queueToEdit.TextureModsToInstall);

                // This must be done after all other content has been inserted!
                RestoreGameBeforeInstall = queueToEdit.RestoreBeforeInstall;

                // Experimental: Load mount priority value here for UI
                foreach (var mod in queueToEdit.ModsToInstall.Where(x => x.Mod != null))
                {
                    mod.Mod.EXP_GetModMountPriority();
                }
            }


        }

        public bool RestoreGameBeforeInstall { get; set; }

        public void OnRestoreGameBeforeInstallChanged()
        {
            // This ensures no duplicates are ever entered.
            ModsInGroup.Remove(new BatchGameRestore());

            if (RestoreGameBeforeInstall)
            {
                ModsInGroup.Insert(0, new BatchGameRestore());
            }
        }

        public ICommand CancelCommand { get; set; }
        public ICommand SaveAndCloseCommand { get; set; }
        public ICommand CheckForIssuesCommand { get; set; }
        public ICommand RemoveFromInstallGroupCommand { get; set; }
        public ICommand AddToInstallGroupCommand { get; set; }
        public ICommand MoveUpCommand { get; set; }
        public ICommand MoveDownCommand { get; set; }
        public ICommand AutosortCommand { get; set; }
        public ICommand AddCustomMEMModCommand { get; set; }
        public ICommand SortByMountPriorityCommand { get; set; }

        private void LoadCommands()
        {
            CancelCommand = new GenericCommand(CancelEditing);
            SaveAndCloseCommand = new GenericCommand(SaveAndClose, CanSave);
            CheckForIssuesCommand = new GenericCommand(CheckForIssues);
            RemoveFromInstallGroupCommand = new GenericCommand(RemoveContentModFromInstallGroup, CanRemoveFromInstallGroup);
            AddToInstallGroupCommand = new GenericCommand(AddModToInstallGroup, CanAddToInstallGroup);
            MoveUpCommand = new GenericCommand(MoveUp, CanMoveUp);
            MoveDownCommand = new GenericCommand(MoveDown, CanMoveDown);
            // AutosortCommand = new GenericCommand(Autosort, CanAutosort);
            AddCustomMEMModCommand = new GenericCommand(ShowMEMSelector, CanAddMEMMod);
            SortByMountPriorityCommand = new GenericCommand(SortByMountPriority, CanSortByMountPriority);
        }

        private bool CanSortByMountPriority()
        {
            return ModsInGroup.Any(x => x is BatchMod m && m.Mod != null);
        }

        private void LeftSideMod_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Double click to install feature.
            // Code is written in nested if statement to make breakpoint easier.
            if (sender is FrameworkElement fwe && fwe.DataContext is Mod)
            {
                AddModToInstallGroup();
            }
        }
        private void SortByMountPriority()
        {
            var shouldContinue = M3L.ShowDialog(this,
                "Sorting will re-order all content mods in your install group. This is an experimental feature and may not properly order your mods.",
                "Experimental feature", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
            if (!shouldContinue)
                return;



            var contentMods = ModsInGroup.OfType<BatchMod>().Where(x => x.Mod != null)
                .OrderByDescending(x => x.Mod.EXP_GetModMountPriority()).ToList(); // Just order it here too. Its reversed ordered as the order reverses again when we insert it
            ModsInGroup.RemoveRange(contentMods);
            foreach (var m in contentMods)
            {
                ModsInGroup.Insert(0, m);
            }

            // Trigger this again
            OnRestoreGameBeforeInstallChanged();
        }

        public void CheckForIssues()
        {
            List<string> possibleIssues = new List<string>();
            var installedDLC = new CaseInsensitiveDictionary<MetaCMM>();
            var mods = ModsInGroup.OfType<BatchMod>()
                .Where(x => x.Mod != null && x.Mod.ParsedModVersion != null).Select(x => x.Mod).ToList();

            // In-order pass for requirements
            foreach (var mod in mods)
            {
                var dlc = mod.GetAllPossibleCustomDLCFolders();
                foreach (var d in dlc)
                {
                    var versionedDLC = new MetaCMM() { Version = mod.ParsedModVersion.ToString() };
                    installedDLC[d] = versionedDLC;
                }

                foreach (var req in mod.RequiredDLC)
                {
                    if (!req.IsRequirementMet(null, installedDLC, checkOptionKeys: false))
                    {
                        var tpmi = TPMIService.GetThirdPartyModInfo(req.DLCFolderName.Key, mod.Game);
                        possibleIssues.Add($"{mod.ModName} requires mod {tpmi?.modname ?? req.DLCFolderName.Key}{(req.MinVersion != null ? " with minimum version " + req.MinVersion : null)} to be installed beforehand, but in this install group it is not");
                    }
                }
            }

            // Check incompatible DLCs in second pass once we know the list of all DLC folders.
            foreach (var mod in mods)
            {
                foreach (var inc in mod.IncompatibleDLC)
                {
                    if (installedDLC.ContainsKey(inc))
                    {
                        var tpmi = TPMIService.GetThirdPartyModInfo(inc, mod.Game);
                        possibleIssues.Add($"{mod.ModName} is not compatible with mod {tpmi?.modname ?? inc}");
                    }
                }
            }

            if (possibleIssues.Any())
            {
                ListDialog ld = new ListDialog(possibleIssues, "Possible issues found",
                    "Mod Manager found potential issues with your install group. This is a best effort guess; it may not be accurate, and it will only catch simple issues.",
                    this);
                ld.ShowDialog();
            }
            else
            {
                M3L.ShowDialog(this, "Mod Manager did not find issues with your install group. This is a best effort guess; it may not be accurate, and it will only catch simple issues.", "No issues detected", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ShowMEMSelector()
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Filter = M3L.GetString(M3L.string_massEffectModderFiles) + @" (*.mem)|*.mem", // Todo: Localize this properly
                Title = M3L.GetString(M3L.string_selectMemFile),
                Multiselect = true,
            };

            var result = ofd.ShowDialog();
            if (result == true)
            {
                foreach (var f in ofd.FileNames)
                {
                    var memFileGame = ModFileFormats.GetGameMEMFileIsFor(f);
                    if (memFileGame != SelectedGame)
                    {
                        // TODO: UPDATE LOCALIZATION TO INCLUDE FILENAME
                        M3L.ShowDialog(this,
                            M3L.GetString(M3L.string_interp_dialog_memForDifferentGame, SelectedGame, memFileGame),
                            M3L.GetString(M3L.string_wrongGame), MessageBoxButton.OK, MessageBoxImage.Error);
                        continue;
                    }

                    // User selected file
                    MEMMod m = new MEMMod()
                    {
                        FilePath = f
                    };

                    m.ParseMEMData();

                    VisibleFilteredMEMMods
                        .Add(m); //Todo: Check no duplicates in left list (or existing already on right?)
                }
            }
        }

        private bool CanAddMEMMod()
        {
            return true;
        }

        private bool CanAutosort()
        {
            return ModsInGroup.Count > 1;
        }

        class ModDependencies
        {

            public List<string> HardDependencies = new List<string>(); // requireddlc

            /// <summary>
            /// List of DLC folders the mod can depend on for configuration.
            /// </summary>
            public List<string> DependencyDLCs = new List<string>();

            /// <summary>
            /// List of all folders the mod mapped to this can install
            /// </summary>
            public List<string> InstallableDLCFolders = new List<string>();

            /// <summary>
            /// The mod associated with this dependencies
            /// </summary>
            public Mod mod;

            public void DebugPrint()
            {
                Debug.WriteLine($@"{mod.ModName}");
                Debug.WriteLine($@"  HARD DEPENDENCIES: {string.Join(',', HardDependencies)}");
                Debug.WriteLine($@"  SOFT DEPENDENCIES: {string.Join(',', DependencyDLCs)}");
                Debug.WriteLine($@"  DLC FOLDERS:       {string.Join(',', InstallableDLCFolders)}");
            }
        }

        private void Autosort()
        {
            // DOESN'T REALLY WORK!!!!!!!!
            // Just leaving here in the event that someday it becomes useful...

#if DEBUG
            // This attempts to order mods by dependencies on others, with mods that have are not depended on being installed first
            // This REQUIRES mod developers to properly flag their alternates!

            /*var dependencies = new List<ModDependencies>();

            foreach (var mod in ModsInGroup)
            {
                var depends = new ModDependencies();

                // These items MUST be installed first or this mod simply won't install.
                // Official DLC is not counted as mod manager cannot install those.
                depends.HardDependencies = mod.Mod.RequiredDLC.Where(x => !MEDirectories.OfficialDLC(mod.Game).Contains(x.DLCFolderName)).Select(x => x.DLCFolderName).ToList();
                depends.DependencyDLCs = mod.Mod.GetAutoConfigs().ToList(); // These items must be installed prior to install or options will be unavailable to the user.
                var custDlcJob = mod.Mod.GetJob(ModJob.JobHeader.CUSTOMDLC);
                if (custDlcJob != null)
                {
                    var customDLCFolders = custDlcJob.CustomDLCFolderMapping.Keys.ToList();
                    customDLCFolders.AddRange(custDlcJob.AlternateDLCs.Where(x => x.Operation == AlternateDLC.AltDLCOperation.OP_ADD_CUSTOMDLC).Select(x => x.AlternateDLCFolder));
                    depends.InstallableDLCFolders.ReplaceAll(customDLCFolders);
                }

                depends.mod = mod.Mod;
                dependencies.Add(depends);
            }

            var fullList = dependencies;

            var finalOrder = new List<BatchMod>();

            // Mods with no dependencies go first.
            var noDependencyMods = dependencies.Where(x => x.HardDependencies.Count == 0 && x.DependencyDLCs.Count == 0).ToList();
            finalOrder.AddRange(noDependencyMods.Select(x => x.mod));
            dependencies = dependencies.Except(noDependencyMods).ToList(); // Remove the added items

            // Mods that are marked as requireddlc in other mods go next. 
            var requiredDlcs = dependencies.SelectMany(x => x.HardDependencies).ToList();
            var modsHardDependedOn = dependencies.Where(x => x.DependencyDLCs.Intersect(requiredDlcs).Any());


            finalOrder.AddRange(modsHardDependedOn);
            dependencies = dependencies.Except(modsHardDependedOn).ToList(); // Remove the added items

            // Add the rest (TEMP)
            finalOrder.AddRange(dependencies.Select(x => x.mod));


            // DEBUG: PRINT IT OUT

            foreach (var m in finalOrder)
            {
                var depend = fullList.Find(x => x.mod == m.Mod);
                depend.DebugPrint();
            }

            ModsInGroup.ReplaceAll(finalOrder);*/
#endif
        }

        private bool CanRemoveFromInstallGroup() => SelectedInstallGroupMod != null;

        private bool CanAddToInstallGroup()
        {
            if (SelectedTabIndex == TAB_CONTENTMOD) return SelectedAvailableMod != null;
            if (SelectedTabIndex == TAB_ASIMOD) return SelectedAvailableASIMod != null;
            if (SelectedTabIndex == TAB_TEXTUREMOD) return SelectedAvailableMEMMod != null;
            return false;
        }

        private bool CanMoveUp()
        {
            if (SelectedInstallGroupMod is BatchMod)
            {
                var index = ModsInGroup.IndexOf(SelectedInstallGroupMod);
                if (index > 0 && ModsInGroup[index - 1] is BatchMod)
                {
                    return true;
                }
            }
            else if (SelectedInstallGroupMod is MEMMod)
            {
                var index = ModsInGroup.IndexOf(SelectedInstallGroupMod);
                if (index > 0 && ModsInGroup[index - 1] is MEMMod)
                {
                    return true;
                }
            }
            return false;
        }

        private bool CanMoveDown()
        {
            if (SelectedInstallGroupMod is BatchMod)
            {
                var index = ModsInGroup.IndexOf(SelectedInstallGroupMod);
                if (index < ModsInGroup.Count - 1 && ModsInGroup[index + 1] is BatchMod)
                {
                    return true;
                }
            }
            else if (SelectedInstallGroupMod is MEMMod)
            {
                var index = ModsInGroup.IndexOf(SelectedInstallGroupMod);
                if (index < ModsInGroup.Count - 1 && ModsInGroup[index + 1] is MEMMod)
                {
                    return true;
                }
            }
            return false;
        }

        private void MoveDown()
        {
            int numToMove = Keyboard.Modifiers == ModifierKeys.Shift ? 5 : 1; // if holding shift, move 5
            for (int i = 0; i < numToMove; i++)
            {
                if (CanMoveDown() && SelectedInstallGroupMod is BatchMod || SelectedInstallGroupMod is MEMMod)
                {
                    var mod = SelectedInstallGroupMod;
                    var oldIndex = ModsInGroup.IndexOf(SelectedInstallGroupMod);
                    var newIndex = oldIndex + 1;
                    ModsInGroup.RemoveAt(oldIndex);
                    ModsInGroup.Insert(newIndex, mod);
                    SelectedInstallGroupMod = mod;
                }
            }

            ScrollSelectedModIntoView();
        }

        private void MoveUp()
        {
            int numToMove = Keyboard.Modifiers == ModifierKeys.Shift ? 5 : 1; // if holding shift, move 5
            for (int i = 0; i < numToMove; i++)
            {
                if (CanMoveUp() && SelectedInstallGroupMod is BatchMod || SelectedInstallGroupMod is MEMMod)
                {
                    var mod = SelectedInstallGroupMod;
                    var oldIndex = ModsInGroup.IndexOf(SelectedInstallGroupMod);
                    var newIndex = oldIndex - 1;
                    ModsInGroup.RemoveAt(oldIndex);
                    ModsInGroup.Insert(newIndex, mod);
                    SelectedInstallGroupMod = mod;
                }
            }

            ScrollSelectedModIntoView();
        }

        private void ScrollSelectedModIntoView()
        {
            InstallGroupMods_ListBox.ScrollIntoView(SelectedInstallGroupMod);
        }

        private void AddModToInstallGroup()
        {
            if (SelectedTabIndex == TAB_CONTENTMOD)
            {
                foreach (var i in ListBox_ContentMods.SelectedItems.OfType<Mod>().ToList()) // ToList as this will be removing items that will change selection
                {
                    if (VisibleFilteredMods.Remove(i))
                    {
                        var index = ModsInGroup.FindLastIndex(x => x is BatchMod or BatchGameRestore);
                        index++; // if not found, it'll be -1. If found, we will want to insert after.
                        ModsInGroup.Insert(index, new BatchMod(i)); // Put into specific position.
                    }
                }
            }
            else if (SelectedTabIndex == TAB_ASIMOD)
            {
                ASIMod m = SelectedAvailableASIMod;
                if (VisibleFilteredASIMods.Remove(m))
                {
                    ModsInGroup.Add(new BatchASIMod(m));
                }
            }
            else if (SelectedTabIndex == TAB_TEXTUREMOD)
            {
                foreach (var i in ListBox_AvailableTextures.SelectedItems.OfType<MEMMod>().ToList()) // ToList as this will be removing items that will change selection
                {
                    if (VisibleFilteredMEMMods.Remove(i))
                    {
                        if (i is M3MEMMod m3mm) // M3MEMMMod must go first
                        {
                            ModsInGroup.Add(new M3MEMMod(m3mm));
                        }
                        else if (i is MEMMod mm)
                        {
                            ModsInGroup.Add(new MEMMod(mm));
                        }
                    }
                }
            }
        }

        private void RemoveContentModFromInstallGroup()
        {
            var m = SelectedInstallGroupMod;
            var selectedIndex = ModsInGroup.IndexOf(m);

            if (SelectedInstallGroupMod is BatchMod bm && ModsInGroup.Remove(m) && bm.IsAvailableForInstall())
            {
                VisibleFilteredMods.Add(bm.Mod);
            }
            else if (SelectedInstallGroupMod is BatchASIMod bai && ModsInGroup.Remove(bai))
            {
                VisibleFilteredASIMods.Add(bai.AssociatedMod.OwningMod);
            }
            else if (SelectedInstallGroupMod is MEMMod m3ai && ModsInGroup.Remove(m3ai) && m3ai.IsAvailableForInstall()) // covers both types
            {
                VisibleFilteredMEMMods.Add(m3ai);
            }

            // Select next object to keep UI working well
            if (ModsInGroup.Count > selectedIndex)
            {
                SelectedInstallGroupMod = ModsInGroup[selectedIndex];
            }
            else
            {
                SelectedInstallGroupMod = ModsInGroup.LastOrDefault();
            }
        }

        private void SaveAndClose()
        {
            if (SaveModern())
            {
                TelemetryInterposer.TrackEvent(@"Saved Batch Group", new Dictionary<string, string>()
                {
                    { @"Group name", GroupName },
                    { @"Group size", ModsInGroup.Count.ToString() },
                    { @"Game", SelectedGame.ToString() }
                });
                Close();
            }
        }

        private bool SaveModern()
        {
            var queue = new BatchLibraryInstallQueue();
            queue.Game = SelectedGame;
            queue.ModName = GroupName;
            queue.QueueDescription = M3Utilities.ConvertNewlineToBr(GroupDescription);
            queue.RestoreBeforeInstall = RestoreGameBeforeInstall;

            // Content mods
            var mods = new List<BatchMod>();
            foreach (var m in ModsInGroup.OfType<BatchMod>())
            {
                mods.Add(m);
            }
            queue.ModsToInstall.ReplaceAll(mods);

            // ASI mods
            var asimods = new List<BatchASIMod>();
            foreach (var m in ModsInGroup.OfType<BatchASIMod>())
            {
                asimods.Add(m);
            }
            queue.ASIModsToInstall.ReplaceAll(asimods);

            // Texture mods
            var texturemods = new List<MEMMod>();
            foreach (var m in ModsInGroup.OfType<MEMMod>())
            {
                texturemods.Add(m);
            }
            queue.TextureModsToInstall.ReplaceAll(texturemods);

            var queueSavePath = queue.GetSaveName(queue.ModName, true);
            var destExists = File.Exists(queueSavePath);
            if (destExists && Path.GetFileName(queueSavePath) != InitialFileName) // If InitialFileName is null this will indicate new group saving over existing rather than a rename
            {
                var continueRes = M3L.ShowDialog(this,
                                    $"An existing install group with the name '{queue.ModName}' already exists. Saving will overwrite that group.\n\nContinue to save this group?",
                                    "File already exists", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (continueRes != MessageBoxResult.Yes)
                    return false;
            }

            SavedPath = queue.Save(true);


            // File name was changed
            if (InitialFileName != null && !Path.GetFileName(queueSavePath).CaseInsensitiveEquals(InitialFileName))
            {
                File.Delete(Path.Combine(M3LoadedMods.GetBatchInstallGroupsDirectory(), InitialFileName));
            }
            return true;
        }

        /// <summary>
        /// Unused - this is how MM8 (126) saved biq files. For reference only
        /// </summary>
        [Conditional(@"Debug")]
        private void SaveLegacy()
        {
            throw new Exception(@"SaveLegacy() is no longer supported");

#if LEGACYCODE
            // This is only here for reference of how it used to work
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(SelectedGame.ToString());
            sb.AppendLine(GroupName);
            sb.AppendLine(M3Utilities.ConvertNewlineToBr(GroupDescription));
            var libraryRoot = M3LoadedMods.GetModDirectoryForGame(SelectedGame);
            foreach (var m in ModsInGroup.OfType<BatchMod>())
            {
                sb.AppendLine(m.ModDescPath.Substring(libraryRoot.Length + 1)); //STORE RELATIVE!
            }

            var batchfolder = M3LoadedMods.GetBatchInstallGroupsDirectory();
            if (existingFilename != null)
            {
                var existingPath = Path.Combine(batchfolder, existingFilename);
                if (File.Exists(existingPath))
                {
                    File.Delete(existingPath);
                }
            }

            var savePath = "";// GetSaveName(GroupName);
            File.WriteAllText(savePath, sb.ToString());
            SavedPath = savePath;
#endif
        }


        private bool CanSave()
        {
            if (string.IsNullOrWhiteSpace(GroupDescription)) return false;
            if (string.IsNullOrWhiteSpace(GroupName)) return false;
            if (!ModsInGroup.Any()) return false;
            //if (ModsInGroup.OfType<BatchMod>().Any(x => x.Mod == null)) return false; // A batch mod could not be found // Disabled 04/25/2023 - hopefully this works properly?
            if (ModsInGroup.OfType<BatchASIMod>().Any(x => x.AssociatedMod == null)) return false; // A batch asi mod could not be found
            return true;
        }

        private void CancelEditing()
        {
            Close();
        }

        public MEGame SelectedGame { get; set; }

        /// <summary>
        /// Selected right pane mod
        /// </summary>
        public object SelectedInstallGroupMod { get; set; }

        public void OnSelectedInstallGroupModChanged()
        {
            if (SelectedInstallGroupMod is BatchMod { Mod.BannerBitmap: null } m)
            {
                m.Mod.LoadBannerImage(); // Method will check if it's null
            }
        }

        /// <summary>
        /// Selected left pane mod
        /// </summary>
        public Mod SelectedAvailableMod { get; set; }

        /// <summary>
        /// Selected left pane ASI mod
        /// </summary>
        public ASIMod SelectedAvailableASIMod { get; set; }

        /// <summary>
        /// Selected left pane MEM mod. Can be MEMMod or M3MEMMod
        /// </summary>
        public MEMMod SelectedAvailableMEMMod { get; set; }

        /// <summary>
        /// The current selected tab. 0 = content mods, 1 = ASI mods - maybe 2 in future = texture mods?
        /// </summary>
        public int SelectedTabIndex { get; set; }


        public void OnSelectedAvailableModChanged()
        {
            AvailableModText = SelectedAvailableMod?.DisplayedModDescription;
        }

        public void OnSelectedAvailableASIModChanged()
        {
            AvailableModText = SelectedAvailableASIMod?.LatestVersion?.Description;
        }

        public void OnSelectedAvailableMEMModChanged()
        {
            AvailableModText = SelectedAvailableMEMMod?.GetDescription() ?? M3L.GetString(M3L.string_selectATextureMod);
        }

        public void OnSelectedTabIndexChanged()
        {
            if (SelectedTabIndex == TAB_CONTENTMOD) OnSelectedAvailableModChanged();
            if (SelectedTabIndex == TAB_ASIMOD) OnSelectedAvailableASIModChanged();
            if (SelectedTabIndex == TAB_TEXTUREMOD) OnSelectedAvailableMEMModChanged();
        }

        public void OnSelectedGameChanged()
        {
            // Set the selector
            foreach (var selector in Games)
            {
                selector.IsSelected = selector.Game == SelectedGame;
            }

            // Update the filtered list
            if (SelectedGame != MEGame.Unknown)
            {
                VisibleFilteredMods.ReplaceAll(M3LoadedMods.Instance.AllLoadedMods.Where(x => x.Game == SelectedGame));
                if (SelectedGame != MEGame.LELauncher)
                {
                    VisibleFilteredASIMods.ReplaceAll(ASIManager.GetASIModsByGame(SelectedGame).Where(x => x.ShouldShowInUI()));
                    VisibleFilteredMEMMods.ReplaceAll(M3LoadedMods.GetAllM3ManagedMEMs(SelectedGame).Where(x => x.Game == SelectedGame));
                }
                else
                {
                    VisibleFilteredASIMods.ClearEx();
                    VisibleFilteredMEMMods.ClearEx();
                }
            }
            else
            {
                VisibleFilteredMods.ClearEx();
                VisibleFilteredASIMods.ClearEx();
                VisibleFilteredMEMMods.ClearEx();
            }
        }

        private void TryChangeGameTo(MEGame newgame)
        {
            if (newgame == SelectedGame) return; //don't care
            if (ModsInGroup.Count > 0 && newgame != SelectedGame)
            {
                var result = M3L.ShowDialog(this, M3L.GetString(M3L.string_dialog_changingGameWillClearGroup), M3L.GetString(M3L.string_changingGameWillClearGroup), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    ModsInGroup.ClearEx();
                    RestoreGameBeforeInstall = false;
                    SelectedGame = newgame;
                }
                else
                {
                    //reset choice
                    Games.ForEach(x => x.IsSelected = x.Game == SelectedGame);
                }
            }
            else
            {
                SelectedGame = newgame;
            }
        }

        private void GameIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fw && fw.DataContext is MEGameSelector gamesel)
            {
                SetSelectedGame(gamesel.Game);
            }
        }

        private void SetSelectedGame(MEGame game)
        {
            Games.ForEach(x => x.IsSelected = x.Game == game);
            TryChangeGameTo(game);
        }
    }

    /// <summary>
    /// This is nothing but a placeholder object to show that the game will restore before install
    /// </summary>
    public class BatchGameRestore : IBatchQueueMod
    {
        public string UIDescription => "Restores the game to vanilla using a game backup.";

        protected bool Equals(BatchGameRestore other)
        {
            // We are always the same as another object of this type.
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BatchGameRestore)obj);
        }

        public override int GetHashCode()
        {
            return 0;
        }

        // IBatchMod interface
        public bool IsAvailableForInstall() => true;
        public string Hash { get; set; }
        public long Size { get; set; }
    }
}
