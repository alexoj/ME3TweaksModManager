﻿using ByteSizeLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MassEffectModManagerCore.ui;
using ME3Explorer;

namespace MassEffectModManagerCore.modmanager.memoryanalyzer
{
    /// <summary>
    /// Interaction logic for MemoryAnalyzer.xaml
    /// </summary>
    public partial class MemoryAnalyzer : NotifyPropertyChangedWindowBase
    {

        #region Static Reference Adding
        private static readonly List<MemoryAnalyzerObject> TrackedMemoryObjects = new List<MemoryAnalyzerObject>();

        //All calls to this method will be removed in release builds
        [Conditional("DEBUG")]
        public static void AddTrackedMemoryItem(string objectname, WeakReference reference)
        {
            //Force concurrency
            Application.Current.Dispatcher.Invoke(() =>
            {
                TrackedMemoryObjects.Add(new MemoryAnalyzerObject(objectname, reference));
            });
        }

        #endregion

        public ObservableCollectionExtended<MemoryAnalyzerObject> InstancedTrackedMemoryObjects { get; set; } = new ObservableCollectionExtended<MemoryAnalyzerObject>();

        readonly DispatcherTimer dispatcherTimer;

        public MemoryAnalyzer()
        {
            AddTrackedMemoryItem("Memory Analyzer", new WeakReference(this));

            DataContext = this;
            Refresh();
            InitializeComponent();

            //  DispatcherTimer setup
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += automatedRefresh_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 3);
            dispatcherTimer.Start();
        }



        private string _lastRefreshText;
        public string LastRefreshText { get => _lastRefreshText; set => SetProperty(ref _lastRefreshText, value); }
        private string _currentUsageText;
        public string CurrentMemoryUsageText { get => _currentUsageText; set => SetProperty(ref _currentUsageText, value); }

        private void automatedRefresh_Tick(object sender, EventArgs e)
        {
            Refresh();
        }

        private void ForceLargeGC_Click(object sender, RoutedEventArgs e)
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
        }

        private void ForceGC_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
        }

        private void Refresh()
        {
            TrackedMemoryObjects.Where(x => !x.IsAlive()).ToList().ForEach(x => x.RemainingLifetimeAfterGC--);
            TrackedMemoryObjects.RemoveAll(x => !x.IsAlive() && x.RemainingLifetimeAfterGC < 0);
            InstancedTrackedMemoryObjects.ReplaceAll(TrackedMemoryObjects);
            LastRefreshText = "Last refreshed: " + DateTime.Now;
            CurrentMemoryUsageText = "Current process allocation: " + ByteSize.FromBytes(System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64);
            //foreach (var item in InstancedTrackedMemoryObjects)
            //{
            //    item.RefreshStatus();
            //}
        }

        private void CleanUpOldRefs_Click(object sender, RoutedEventArgs e)
        {
            TrackedMemoryObjects.RemoveAll(x => !x.IsAlive());
            InstancedTrackedMemoryObjects.ReplaceAll(TrackedMemoryObjects);
        }

        public class MemoryAnalyzerObject : NotifyPropertyChangedBase
        {
            private readonly WeakReference Reference;
            public string AllocationTime { get; }
            private string _referenceName;
            public System.Windows.Media.Brush DrawColor
            {
                get
                {
                    if (RemainingLifetimeAfterGC < 5)
                    {
                        //Fadeout
                        return new SolidColorBrush(Color.FromArgb((byte)(128 + (RemainingLifetimeAfterGC * 25)), 0, 0, 0));
                    }
                    else
                    {
                        return Brushes.Black;
                    }
                }
            }
            public int RemainingLifetimeAfterGC = 10;
            public string ReferenceName
            {
                get => _referenceName;
                set => SetProperty(ref _referenceName, value);
            }

            public string ReferenceStatus
            {
                get
                {
                    if (Reference.IsAlive)
                    {
                        if (Reference.Target is FrameworkElement w)
                        {
                            return w.IsLoaded ? "In Memory, Open" : "In Memory, Closed";
                        }
                        return "In Memory";
                    }
                    else
                    {
                        return "Garbage Collected";
                    }
                }
            }

            public MemoryAnalyzerObject(string ReferenceName, WeakReference Reference)
            {
                AllocationTime = DateTime.Now.ToString();
                this.Reference = Reference;
                this.ReferenceName = ReferenceName;
            }

            public void RefreshStatus()
            {
                OnPropertyChanged(nameof(ReferenceStatus));
            }

            public bool IsAlive()
            {
                return Reference.IsAlive;
            }
        }

        private void MemoryAnalyzer_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            dispatcherTimer.Stop();
        }
    }
}