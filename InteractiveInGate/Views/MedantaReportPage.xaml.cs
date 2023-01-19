using InteractiveInGate.ViewModels;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Navigation;

namespace InteractiveInGate.Views
{
    /// <summary>
    /// Interaction logic for ItemsPage.xaml
    /// </summary>
    public partial class MedantaReportPage : Page
    {
        private SingletonNavigator navigator;

        private void StoryboardCompleted(object s, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() => CloseMItemsPageButton.Background = (Brush)Application.Current.FindResource("InteractiveInGateClickedBackround"));
            navigator.Navigate(SingletonNavigator.PageView.Start);
        }

        private void ResetState()
        {
            closeMItemsPageClicked = false;
            Application.Current.Dispatcher.Invoke(() => CloseMItemsPageButton.Background = Brushes.Transparent);
        }

        public MedantaReportPage(SingletonNavigator nav)
        {
            navigator = nav;
            InitializeComponent();
            ProgressAnimation.Duration = TimeSpan.FromSeconds(App.Configuration.AutoConfirmTimeout); // Read the timeout from the configurations json or use the default value is missing. TODO: default is 10
            Loaded += (s, e) =>
            {
                ResetState();
                ProgressStoryboard.Completed += StoryboardCompleted;
            };
            void pageUnloaded(object sender, RoutedEventArgs e)
            {
                ProgressStoryboard.Completed -= StoryboardCompleted;
            }
            Unloaded += pageUnloaded;
        }

        private bool closeMItemsPageClicked = false;
        private void CloseMItemsPage_Click(object sender, RoutedEventArgs e)
        {
            /* Guard against multiple CloseItemsPage clicks */
            if (closeMItemsPageClicked)
                return;
            closeMItemsPageClicked = true;

            ProgressStoryboard.Completed -= StoryboardCompleted;
            (sender as Button).Background = (Brush) Application.Current.FindResource("InteractiveInGateClickedBackround");
            navigator.Navigate(SingletonNavigator.PageView.Start);
            e.Handled = true;
        }
    }
}
