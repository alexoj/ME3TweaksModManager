using System.Threading;
using System.Windows.Input;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services.Restore;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.ui;

namespace ME3TweaksModManager.modmanager.usercontrols
{
    /// <summary>
    /// Handler for game auto restore
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class AutoGameRestorePanel : MMBusyPanelBase
    {
        public int Percent { get; private set; }

        public void OnPercentChanged()
        {
            PercentVisible = Percent >= 0;
        }
        public bool PercentVisible { get; private set; }


        public string ActionText { get; private set; }

        private GameTarget _target;

        public AutoGameRestorePanel(GameTarget target)
        {
            _target = target;
        }

        public override void HandleKeyPress(object sender, KeyEventArgs e)
        {
            //autocloses
        }

        public override void OnPanelVisible()
        {
            InitializeComponent();

            NamedBackgroundWorker nbw = new NamedBackgroundWorker(@"AutoGameRestore");
            nbw.DoWork += (a, b) =>
            {
                var restoreController = new GameRestore(_target.Game)
                {
                    BlockingErrorCallback = M3PromptCallbacks.BlockingActionOccurred,
                    ConfirmationCallback = (message, title) => true, // This is automated. All confirmations will be true
                    RestoreErrorCallback = M3PromptCallbacks.BlockingActionOccurred,
                    GetRestoreEverythingString = _ => "", // In automated mode this is not used.
                    UseOptimizedTextureRestore = () => Settings.UseOptimizedTextureRestore,
                    ShouldLogEveryCopiedFile = () => Settings.LogBackupAndRestore,
                    UpdateStatusCallback = x => ActionText = x,
                    UpdateProgressCallback = (x, y) => Percent = (int)(x * 100f / y),
                    SetProgressIndeterminateCallback = x => { if (x) Percent = -1; }
                };
                restoreController.PerformRestore(_target, _target.TargetPath);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                OnClosing(DataEventArgs.Empty);
            };
            nbw.RunWorkerAsync();
        }

        public override bool CanBeForceClosed()
        {
            // This is a restore panel
            return false;
        }

        /// <summary>
        /// Set to true to disable autosizing feature
        /// </summary>
        public override bool DisableM3AutoSizer { get; set; } = true;
    }
}
