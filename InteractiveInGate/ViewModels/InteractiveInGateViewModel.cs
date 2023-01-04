using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Process;
using rfid;
using Synchronize;
using InteractiveInGate.Utils;

namespace InteractiveInGate.ViewModels
{

    internal enum ConnectionStatus
    {
        Online,
        OfflineMode,
        CriticalError
    }

    internal class Item : IComparable<Item>
    {
        internal const string UnknownText = "Unknown items found";
        public Item(SkuGroupItem g)
        {
            // JSa TTR 7.4.2022 include all that is included in App.Configuration.Report.Reporting (not only the 1st item)
            // TODO check this out
            Description = g.text;
            Amount = g.CounterValue();
        }

        public string Description { get; }
        public int Amount { get; }

        public int CompareTo(Item other)
        {
            if (Description == UnknownText) return 1; // Unknown always goes to the end
            if (other.Description == UnknownText) return -1;
            return Description.CompareTo(other.Description); // Sort by description first
        }
    }


    internal partial class InteractiveInGateViewModel : INotifyPropertyChanged
    {
        private static readonly string CriticalError = Application.Current.FindResource("CriticalError") as string;
        private static readonly string OfflineMode = Application.Current.FindResource("OfflineMode") as string;


        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private SelfDiagnosticts selfDg;    // RAD-1206

        private int inventoryCount;
        private string errorString;
        private Executor executor;
        private CancellationTokenSource cts;
        private volatile TaskCompletionSource<byte> CancellingProgress;

        private LocationNode currentLocation;
        public LocationNode CurrentLocation
        {
            get => currentLocation;
            private set
            {
                currentLocation = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentLocation"));
            }
        }
        private string currentLocationName;
        public string CurrentLocationName
        {
            get => currentLocationName;
            set
            {
                currentLocationName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentLocationName"));
            }
        }

        internal readonly Gate process;

        public BulkObservableCollection<Item> Items { get; }
        public event PropertyChangedEventHandler PropertyChanged;

        internal void Cancel()
        {
            if (CancellingProgress != null)
                throw new InvalidOperationException("Can't cancel progress multiple times");
            CancellingProgress = new TaskCompletionSource<byte>();
            cts.Cancel();
        }

        internal async Task WaitCancelAsync()
        {
            await CancellingProgress.Task;
            CancellingProgress = null;
        }


        public int ItemsTotalCount { get => Items.Sum(i => i.Amount); }

        public string ErrorString
        {
            get => errorString;
            private set
            {
                if (errorString == value)
                    return;
                errorString = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ErrorString"));
            }
        }

        public InteractiveInGateViewModel(SelfDiagnosticts selfDiagnosticts)
        {
            var localization = Thread.CurrentThread.CurrentCulture.Name;
            selfDg = selfDiagnosticts;

            Items = new BulkObservableCollection<Item>();

            executor = Executor.FromConfig(App.Configuration.Executor);
            process = executor.Process.First(p => p is Gate) as Gate;

            // Tell process that we are running a Track and Trace gate - in contrast to RouterGate
            // process.IsTtrGate = true;
            // TODO: Tell process that we are running a IIG - in contrast to RouterGate or Track and Trace gate 
            // process.IsIiGate = true;
            // REPLACED by operating_mode configuration setting

            Task.Run(async () => // Poll offline status every minute
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    ErrorString = process.IsOffline ? OfflineMode : null;
                }
            });
        }

        public int InventoryCount
        {
            get => inventoryCount;
            private set
            {
                if (inventoryCount != value)
                {
                    inventoryCount = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("InventoryCount"));
                }
            }
        }

        private void CountUpdated(int c, Gate.ScanState s)
        {
            InventoryCount = c;
            if (s == Gate.ScanState.RADEA_COMMUNICATION)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ProgressRadea"));
        }

        public async Task StartAsync(LocationNode location, string name)
        {
            CurrentLocationName = name;
            CurrentLocation = location;
            InventoryCount = 0;
            rfidEntryList tags = null;
            cts = new CancellationTokenSource();
            try
            {
                if (InteractiveInGate.App.Configuration.StreamInventory)
                    tags = await process.StartStreamAsync(cts.Token, (c, s) => CountUpdated(c, s), location.Uuid); // Start scan and update inventory count
                else
                    tags = await process.StartAsync(cts.Token, (c, s) => CountUpdated(c, s), location.Uuid); // Start scan and update inventory count
            }
            catch (System.OperationCanceledException)
            {
                logger.Info(() => "Tags scanning cancelled");
                CancellingProgress.TrySetResult(1);
                return;
            }
            catch (Exception ex)
            {
                logger.Info(() => $"Unknown exception {ex} during scan");
                if (CancellingProgress != null)
                    CancellingProgress.TrySetResult(1);
                return;
            }
            finally
            {
                NLog.LogManager.Flush();
            }

            logger.Debug("Got " + tags.ItemStorage.Count + " tags from readout.");

            SkuGroupReport report = new SkuGroupReport(App.Configuration.Report.Grouping, App.Configuration.Report.Reporting) { NullSkuText = Item.UnknownText };
            foreach (var epc in tags?.ItemStorage?.Keys?.Select(tag => tag.ToString()))
            {
                process.EpcToSku(epc, out Sku sku, out _);
                report.Update(sku, epc);
            }
            var items = new List<Item>();
            report.ForEach(g => items.Add(new Item(g)));
            items.Sort();
            Items.BeginBulkOperation();
            Items.Clear();
            items.ForEach(i => Items.Add(i));
            Items.EndBulkOperation();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ItemsTotalCount"));

            logger.Debug("Items after StartAsync: ");
            foreach (Item item in Items)
            {
                logger.Debug(item.Description + ": " + item.Amount);
            }
        }
    }
}
