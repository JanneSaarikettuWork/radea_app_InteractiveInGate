using InteractiveInGate.Properties;
using InteractiveInGate.Views;
using MahApps.Metro.Controls;
using System;

using InteractiveInGate.Models.Json;

using System.ComponentModel;
using System.Deployment.Application;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MahApps.Metro.Controls.Dialogs;

namespace InteractiveInGate
{
    public partial class App : Application
    {
        public static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static Mutex mutex = new Mutex(false, "{E45C634B-D90E-44D2-90D5-B28905F191B6}");
        private Timer updateTimer;

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

            var process = System.Diagnostics.Process.GetCurrentProcess();
            return new StringBuilder()
                .AppendFormat("\nName:\t\t{0}\n", process.ProcessName)
                .AppendFormat("Configuration:\t{0}\n", Path.GetFullPath(conf))
                .AppendFormat("Version:\t\t{0}\n", version)
                .AppendFormat("Started:\t\t{0}\n", process.StartTime)
                .AppendFormat("Uptime:\t\t{0}\n", DateTime.UtcNow - process.StartTime.ToUniversalTime())
                .AppendFormat("Self-update:\t{0}\n", Configuration.SelfUpdateTimeout)
                .AppendFormat("Hostname:\t{0}\n", Environment.MachineName)
                .AppendFormat("Build info:\t{0}{1}\n", InteractiveInGate.Properties.Resources.BuildDate.Replace(Environment.NewLine, ""),
#if DEBUG
                "DEBUG"
#else
                "RELEASE"
#endif
                )
                .ToString();

        }
        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            var activationData = Environment.MachineName + "_routergate.json";
            try
            {
                activationData = String.Join(" ", AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData);
                Logger.Info("Using appref-ms parameters {0}", activationData);
            }
            catch (Exception)
            {
                Logger.Info("Using exe parameters {0}", String.Join(" ", e.Args));
            }

            conf = e.Args.Length > 0 ? e.Args[0] : activationData;
            Logger.Info($"Loading configuration from {conf}");
            //var builder = new ConfigurationBuilder()
            //     .SetBasePath(Directory.GetCurrentDirectory())
            //     .AddJsonFile(conf);
            //Configuration = builder.Build();
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

            updateTimer = new Timer(_ => UpdateCheck(conf), 0, TimeSpan.Zero, TimeSpan.FromHours(Configuration.SelfUpdateTimeout));
        }

        private void UpdateCheck(string conf)
        {
            try
            {
                Logger.Info("Checking for update...");
                if (!ApplicationDeployment.IsNetworkDeployed)
                {
                    Logger.Info("Application is not network deployed and therefore cannot be updated.");
                    return;
                }
                var ad = ApplicationDeployment.CurrentDeployment;
                if (!ad.CheckForDetailedUpdate().UpdateAvailable)
                {
                    Logger.Info("Update not found.");
                    return;
                }
                Logger.Info("Update found.");
                ad.Update();
                Logger.Info("Self-update done from version {0} to {1}.", ad.CurrentVersion, ad.UpdatedVersion);
                var assembly = Assembly.GetExecutingAssembly();
                var restartApp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), // 
                    (assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false)[0] as AssemblyProductAttribute)?.Product,
                    (assembly.GetCustomAttributes(typeof(AssemblyTitleAttribute), false)[0] as AssemblyTitleAttribute)?.Title + ".appref-ms");
                Logger.Info("Restarting... ({0} {1})", restartApp, conf);
                System.Diagnostics.Process.Start(restartApp, conf);
                Dispatcher.Invoke(() => ShutdownApp(), DispatcherPriority.Normal);

            }
            catch (Exception e)
            {
                Logger.Info("Cannot check for update: {0}", e.Message);
            }
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
