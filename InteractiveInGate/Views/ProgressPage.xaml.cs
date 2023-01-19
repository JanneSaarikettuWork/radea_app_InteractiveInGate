using InteractiveInGate.ViewModels;
using System;
using System.Collections.Specialized;
using static System.Globalization.CultureInfo;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Threading.Tasks;
using MahApps.Metro.Controls.Dialogs;
using System.Diagnostics;

namespace InteractiveInGate.Views
{
    /// <summary>
    /// Interaction logic for ProgressPage.xaml
    /// </summary>
    public partial class ProgressPage : Page
    {
        private SingletonNavigator navigator;
        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private Stopwatch RadeaBundlingStopwatch = new Stopwatch();
        private bool RadeaBundling = false;

        void StoryboardCompleted(object s, EventArgs e)
        {
            navigator.Navigate(SingletonNavigator.PageView.Start);
            (App.Current.MainWindow as MainWindow).ShowModalMessageExternal(Application.Current.Resources["TagsNotFound"] as string, null);
        }

        private void ResetState()
        {
            AlreadyCancelled = false;
            CancelButton.Visibility = Visibility.Visible;
            Application.Current.Dispatcher.Invoke(() => ProgressRing.Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("InteractiveInGateBlue"));
            Application.Current.Dispatcher.Invoke(() => Progress.Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("InteractiveInGateBlue"));
            RadeaBundling = false;
        }

        public ProgressPage(SingletonNavigator nav)
        {
            navigator = nav;
            InitializeComponent();
            ProgressAnimation.Duration = TimeSpan.FromSeconds(App.Configuration.TagsWaitTimeout); // Read the timeout from the configurations json or use the default value is missing. TODO: default 60
            Loaded += (s, e) =>
            {
                ResetState();
                ProgressStoryboard.Completed += StoryboardCompleted;

                var vm = DataContext as InteractiveInGateViewModel;

                void itemsChanged(object obj, NotifyCollectionChangedEventArgs ev)
                {
                    RadeaBundlingStopwatch.Stop();
                    var total = String.Format("{0:F3}", RadeaBundlingStopwatch.ElapsedMilliseconds / 1000.0);

                    Logger.Info($"Radea bundling took {total} s");

                    bool MedantaReport = true;

                    if (MedantaReport == false)
                        navigator.Navigate(SingletonNavigator.PageView.Items);
                    else
                        navigator.Navigate(SingletonNavigator.PageView.Medanta);
                }

                vm.Items.CollectionChanged += itemsChanged;
                vm.PropertyChanged += OnPropertyChangedHandler;
                Unloaded += (_, __) =>
                {
                    ProgressStoryboard.Completed -= StoryboardCompleted;
                    vm.Items.CollectionChanged -= itemsChanged;
                    vm.PropertyChanged -= OnPropertyChangedHandler;
                };
            };
        }

        private bool AlreadyCancelled = false;
        private async void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (AlreadyCancelled)
                return;
            AlreadyCancelled = true;
            var vm = DataContext as InteractiveInGateViewModel;
            Application.Current.Dispatcher.Invoke(() => ProgressRing.Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("InteractiveInGateCancel"));
            Application.Current.Dispatcher.Invoke(() => Progress.Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("InteractiveInGateCancel"));
            ProgressStoryboard.Completed -= StoryboardCompleted;
            vm.Cancel();
            CancelButton.Visibility = Visibility.Hidden;
            await vm.WaitCancelAsync();
            navigator.Navigate(SingletonNavigator.PageView.Start);
        }

        private void OnPropertyChangedHandler(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ProgressRadea")
            {
                if (!RadeaBundling)
                {
                    RadeaBundlingStopwatch.Restart();
                    RadeaBundling = true;
                }
                Application.Current.Dispatcher.Invoke(() => ProgressRing.Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("InteractiveInGatePropertyChanged"));
                Application.Current.Dispatcher.Invoke(() => Progress.Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("InteractiveInGatePropertyChanged"));
                Application.Current.Dispatcher.Invoke(() => InventoryCountTextBlock.FontWeight = FontWeights.Bold);
            }
        }
    }
}
