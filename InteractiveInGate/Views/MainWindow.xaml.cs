using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using InteractiveInGate.Properties;
using InteractiveInGate.ViewModels;
using System;
using System.Collections.Generic;
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
using System.Windows.Threading;
using NLog.Fluent;

namespace InteractiveInGate.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private SelfDiagnosticts selfDiagnosticts;
        public SingletonNavigator Navigator { get; set; }

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            Topmost = false;
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
#endif
        }

        private void Logo_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowStyle = WindowStyle == WindowStyle.None ? WindowStyle.SingleBorderWindow : WindowStyle.None; // toggle showing/hiding minimize/maximize window buttons
                WindowState = WindowState.Maximized;
                // TODO Kokeillaas taas toimisiko tämä nyt uudessa MahApp kirjastossa
                this.ShowModalMessageExternal("Version", ((App)Application.Current).GetInfo());
                // MessageBox.Show(((App)Application.Current).GetInfo(), " Version ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InteractiveInGateViewModel gate;

            try
            {
                selfDiagnosticts = new SelfDiagnosticts();
                gate = new InteractiveInGateViewModel(selfDiagnosticts);
            }
            catch (Exception ex)
            {
                NLog.LogManager.GetCurrentClassLogger().Error(ex);
                this.ShowModalMessageExternal(Application.Current.FindResource("CriticalError") as string, ex.Message);
                NLog.LogManager.Shutdown();
                Application.Current.Shutdown();
                return;
            }

            DataContext = gate;


            Navigator = new SingletonNavigator(RootFrame);
            Navigator.Navigate(SingletonNavigator.PageView.Start);
            Logo.Source = new BitmapImage(new Uri("../Assets/Nordic_ID2.png", UriKind.RelativeOrAbsolute));

            gate.RegisterReader();
            selfDiagnosticts.Start(gate);
        }

        private void Frame_LoadCompleted(object sender, NavigationEventArgs e)
        {
            UpdateFrameDataContext();
        }

        private void Frame_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateFrameDataContext();
        }

        private void UpdateFrameDataContext()
        {
            if (RootFrame.Content is FrameworkElement content)
                content.DataContext = RootFrame.DataContext;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            NLog.LogManager.Shutdown();
            Application.Current.Shutdown();
        }

        public int LookupSize
        {
            get { return (DataContext != null ? ((InteractiveInGateViewModel)DataContext).LookupSize : 0); }
        }

    }
}
