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
    public partial class ItemsPage : Page
    {
        private SingletonNavigator navigator;

        private void StoryboardCompleted(object s, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() => CloseItemsPageButton.Background = (Brush)Application.Current.FindResource("InteractiveInGateClickedBackround"));
            navigator.Navigate(SingletonNavigator.PageView.Start);
        }

        private void ResetState()
        {
            closeItemsPageClicked = false;
            Application.Current.Dispatcher.Invoke(() => CloseItemsPageButton.Background = Brushes.Transparent);
        }

        public ItemsPage(SingletonNavigator nav)
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

        private bool closeItemsPageClicked = false;
        private void CloseItemsPage_Click(object sender, RoutedEventArgs e)
        {
            /* Guard against multiple CloseItemsPage clicks */
            if (closeItemsPageClicked)
                return;
            closeItemsPageClicked = true;

            ProgressStoryboard.Completed -= StoryboardCompleted;
            (sender as Button).Background = (Brush) Application.Current.FindResource("InteractiveInGateClickedBackround");
            navigator.Navigate(SingletonNavigator.PageView.Start);
            e.Handled = true;
        }
    }

    public class HeightConverter : IMultiValueConverter
    {
        #region IMultiValueConverter Members
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double totalHeight = System.Convert.ToDouble(values[0]);
            int count = System.Convert.ToInt32(values[1]);
            count = count <= 10 ? 11 : count <= 20 ? count + 1 : 21; // TODO: fix this workaround that does not allow vertical scrollbox to appear for 5 and 10 items.
            return totalHeight / count;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        #endregion
    }

}
