using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
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
using static System.Net.Mime.MediaTypeNames;

namespace InteractiveInGate.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InteractiveInGateViewModel gate;

            //try
            //{
            //    selfDiagnosticts = new SelfDiagnosticts();
            //    gate = new RouterGateViewModel(selfDiagnosticts);
            //}
            //catch (Exception ex)
            //{
            //    NLog.LogManager.GetCurrentClassLogger().Error(ex);
            //    this.ShowModalMessageExternal(Application.Current.FindResource("CriticalError") as string, ex.Message);
            //    NLog.LogManager.Shutdown();
            //    Application.Current.Shutdown();
            //    return;
            //}

            //DataContext = gate;

            //Navigator = new SingletonNavigator(RootFrame);
            //Navigator.Navigate(SingletonNavigator.PageView.Start);
            //Logo.Source = new BitmapImage(new Uri("../Assets/Comforta RGB.png", UriKind.RelativeOrAbsolute));

            //gate.RegisterReader();
            //selfDiagnosticts.Start(gate);
        }


    }
}
