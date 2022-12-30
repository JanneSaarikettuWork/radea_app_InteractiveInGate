using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using InteractiveInGate.Properties;
using InteractiveInGate.Views;
using System;
using System.Deployment.Application;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

using InteractiveInGate.Models.Json;
using System.ComponentModel;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Threading.Tasks;

namespace InteractiveInGate
{
    public partial class App : Application
    {
        public static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static Mutex mutex = new Mutex(false, "{E45C634B-D90E-44D2-90D5-B28905F191B6}");

        private string conf = null;

        static public Config Configuration;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {

            if (!mutex.WaitOne(TimeSpan.Zero, false))
            {
                Logger.Info("Application is already running. Exiting...");
                ShutdownApp();
            }
            else
            {
                Logger.Info("Started");
                InitializeComponent();
                DispatcherUnhandledException += OnDispatcherUnhandledException;
            }
        }

        [STAThread]
        static void Main()
        {
            new SplashScreen("Assets/Nordic_ID.jpg").Show(true);
            try
            {
                mutex.WaitOne(TimeSpan.Zero, true);
            }
            catch (Exception)
            {
                Logger.Fatal(() => "Abandoned mutex prevent the app to run correctly. Releasing mutex and proceeding with the application start..");
                mutex.ReleaseMutex();
            }

            try
            {
                if (mutex.WaitOne(TimeSpan.Zero, true))
                {
                    App app = new App();
                    app.Run(new MainWindow());
                    mutex.ReleaseMutex();
                }
                else
                    Logger.Info(() => "Application is already running.");
            }
            catch (Exception e)
            {
                Logger.Fatal(() => $"Caught exception starting the app: {e.Message} {e}");
            }
        }


        internal string GetInfo()
        {
            var ret = new StringBuilder();
            string version = null;
            try
            {
                version = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
            }
            catch (Exception)
            {
                version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }

            var proc = System.Diagnostics.Process.GetCurrentProcess();
            return new StringBuilder()
                .AppendFormat("\nName:\t\t{0}\n", proc.ProcessName)
                .AppendFormat("Configuration:\t{0}\n", Path.GetFullPath(conf))
                .AppendFormat("Version:\t\t{0}\n", version)
                .AppendFormat("Started:\t\t{0}\n", proc.StartTime)
                .AppendFormat("Uptime:\t\t{0}\n", DateTime.UtcNow - proc.StartTime.ToUniversalTime())
                .AppendFormat("Hostname:\t{0}\n", Environment.MachineName)
                .AppendFormat("Build info:\t{0}{1}\n", InteractiveInGate.Properties.Resources.BuildDate.Replace(Environment.NewLine, ""),
#if DEBUG
                "DEBUG"
#else
                "RELEASE"
#endif
                )
                .AppendFormat("Configuration:\t{0}\n", Path.GetFullPath(conf))
                .AppendFormat("Lookup used:\t{0}\n", Configuration?.Executor?.Process[0]?.RadeaLookup == true ? "yes" : "no")
                .AppendFormat("Lookup size:\t{0}\n", (this?.MainWindow as InteractiveInGate.Views.MainWindow)?.LookupSize)
                .ToString();

        }
        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            var activationData = Environment.MachineName + "_iigate.json";

            try
            {
                // Do not throw exceptios as part of normal application control - it is ugly 
                // activationData = String.Join(" ", AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData);

                if (AppDomain.CurrentDomain.SetupInformation.ActivationArguments != null)
                {
                    activationData = String.Join(" ", AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData);
                    Logger.Info("Using appref-ms parameters {0}", activationData);
                }
               
            }
            catch (Exception ex)
            {
                // Logger.Info("Using exe parameters {0}", String.Join(" ", e.Args));
                Logger.Error("Error in determining application activation arguments", ex.Message);
            }

            conf = e.Args.Length > 0 ? e.Args[0] : activationData;
            Logger.Info($"Loading configuration from {conf}");

            try
            {
                Configuration = Config.FromJson(File.ReadAllText(conf));
            }
            catch (Exception confex)
            {
                Logger.Fatal($"Configuration file parsing failed: ${confex}");
            }

            Logger.Info("Application Info: {0}", GetInfo());
            CultureInfo ci = CultureInfo.CreateSpecificCulture(Configuration.Localization ?? "en-US");
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;
            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;
            //Resources.MergedDictionaries.Clear();
            Resources.MergedDictionaries.Add(new ResourceDictionaryLocator());
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Fatal(e.ToString());
            try
            {
                (this.MainWindow as MetroWindow).ShowModalMessageExternal("FATAL", e.ToString());
            }
            catch (Exception newe)
            {
                Logger.Error("Can't open error window for showing unhandled exception: {0} original exception {1}", newe.ToString(), e);
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            Logger.Info("Application closes");
        }

        private void ShutdownApp()
        {
            NLog.LogManager.Shutdown();
            Shutdown();
        }
    }
}
