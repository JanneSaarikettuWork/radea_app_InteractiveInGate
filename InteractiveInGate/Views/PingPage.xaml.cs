using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using InteractiveInGate.ViewModels;

namespace InteractiveInGate.Views
{
    /// <summary>
    /// Interaction logic for PingPage.xaml
    /// </summary>
    public partial class PingPage : Page
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private System.Timers.Timer timer;
        private Stopwatch stopWatch;
        private Process.Process process;
        private SingletonNavigator navigator;

        private void Click(object sender, MouseButtonEventArgs e)
        {
            navigator.Navigate(SingletonNavigator.PageView.Start);
        }

        private void Pinger(object sender, EventArgs e)
        {
            try
            {
                var d = process.PingReader();
                if (d < 0)
                {
                    d = double.NaN;
                }
                SeriesCollection[0].Values.Add(new ObservablePoint(stopWatch.ElapsedMilliseconds/1000.0, d*1000.0));
                stopWatch.Start();
            }
            catch (Exception ex)
            {
                logger.Info(() => $"Pinger exception: {ex.ToString()}");
            }
        }

        public PingPage(SingletonNavigator nav)
        {
            navigator = nav;
            InitializeComponent();
            stopWatch = new Stopwatch();
            timer = new System.Timers.Timer(1000);
            timer.Elapsed += Pinger;

            Loaded += (_, __) =>
            {
                var vm = DataContext as InteractiveInGateViewModel;
                process = vm.process;
                SeriesCollection[0].Values = new ChartValues<ObservablePoint> { };
                stopWatch.Reset();
                timer.Start();

                Unloaded += (___, ____) =>
                {
                    timer.Stop();
                    stopWatch.Stop();
                };
            };

            SeriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "NurApi ping",
                    Values = new ChartValues<ObservablePoint> { }
                }
            };

            YFormatter = value => value.ToString("f", System.Globalization.CultureInfo.InvariantCulture);

            CartesianChart.Series = SeriesCollection;
            AxisY.LabelFormatter = YFormatter;

            DataContext = this;
        }
        public SeriesCollection SeriesCollection { get; set; }
        public Func<double, string> YFormatter { get; set; }
    }
}
