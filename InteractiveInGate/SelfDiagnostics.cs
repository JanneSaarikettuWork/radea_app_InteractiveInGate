using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace InteractiveInGate.ViewModels
{

    internal partial class InteractiveInGateViewModel
    {
        static int TRY_COUNT = 3;    // In registering reader and reporting antenna data to Radea
        static int WAIT_BETWEEN_RETRY = 1000; // 1000 ms = 1 s

        /// <summary>
        /// Register reader. If reader physical HW instance (serial number) is changed, new reader uuid is created - othervise existing uuid is used
        /// </summary>
        public void RegisterReader()
        {
            int tryCount = TRY_COUNT;
            Exception exception = null;
            string appName = "";

            Multireader.Reader reader = null;

            while (tryCount-- > 0)
            {
                try
                {
                    appName = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title;

                    // Assume 1 reader
                    reader = executor.Multireader.Readers.First();
                    var readerHwInstanceIdentifier = reader.GetReaderHwInstanceIdentifier();

                    string locationUuid_WhereReaderReports = process.RouterGateLocationUuid.ToString();

                    var radea = process.Radea;
                    string registeredReaderUUID = radea.RegisterApplicationReader(appName,
                                                                                   readerHwInstanceIdentifier.SerialNumber,
                                                                                   readerHwInstanceIdentifier.ReaderType,
                                                                                   locationUuid_WhereReaderReports,
                                                                                   null); 

                    if (string.IsNullOrWhiteSpace(registeredReaderUUID)) // registering failed
                    {
                        string errorString = $"RegisterApplicationReader() returned null for application {appName} with reader \"{readerHwInstanceIdentifier.ReaderType}{readerHwInstanceIdentifier.SerialNumber}\". Retrying..";
                        logger.Error(errorString);
                        Task.Delay(WAIT_BETWEEN_RETRY);
                        continue;
                    }
                    else // we got a proper uuid
                    {
                        string infoString = $"Reader \"{readerHwInstanceIdentifier.ReaderType}{readerHwInstanceIdentifier.SerialNumber}\" for application {appName} successfully registered with uuid \"{registeredReaderUUID}\"";
                        logger.Info(infoString);

                        string originalConfiguredReaderUuid = process.ReaderUuid;

                        if (registeredReaderUUID != originalConfiguredReaderUuid)
                        {
                            string warningStrig = $"Configured reader uuid \"{originalConfiguredReaderUuid}\" differs from registered uuid \"{registeredReaderUUID}\". Using the latter.";
                            logger.Warn(warningStrig);
                            process.ReaderUuid = registeredReaderUUID;
                        }

                        return; // Todos bien, como en Strömsö
                    }
                }
                catch (Exception ex)
                {
                    string errorString = $"RegisterApplicationReader() for application {appName} with reader \"{(reader?.Hostname) ?? "(null)"}\" threw exception {ex.Message}";
                    logger.Error(errorString);
                    exception = ex;
                    Task.Delay(WAIT_BETWEEN_RETRY);
                    continue;
                }
            } // while()

            // failure of some sort
            string exceptionString = "RegisterApplicationReader() - could not successfully register reader. ";

            if (exception == null) // registering failed due to a less fatal problem, no exception
            {
                throw (new Exception(exceptionString));

                /* Note: here failing in registration is considered severe problem and an exception is thrown. Registration is done
                 * in the beginning of the program run and at least then the Radea connection shoud work.
                 *  - - -
                 * Other, less strict approach could be that we just proceed with the file-configured reader uuid 
                 * (originalConfiguredReaderUuid) if registering failed and we did not get a proper registeredReaderUUID value.
                 * Kari: e.g. to collect tags to cache file if communication to Radea is down.
                 */
                // return;
            }
            else // registering failed due to an exception (attach it as inner exception and throw)
                throw (new Exception(exceptionString, exception));
        }

        /// <summary>
        /// Check and return diagnostics state of the reader(s) 
        /// </summary>
        /// <returns></returns>
        public int CheckReaderConnections()
        {
            try
            {
                var Readers = executor.Multireader.Readers;
                foreach (var Reader in Readers)
                {
                    string pingreply = Reader.Ping();

                    if (pingreply == "timeout" || pingreply == "unknown")
                        return DiagnostictsState.StateError;

                    if (false == Reader.Connected())
                        return DiagnostictsState.StateWarning;
                }
                return DiagnostictsState.StateOk; // reader (or all readers, if many) was connected
            }
            catch (Exception ex)
            {
                logger.Debug($"Got exception in CheckReaderConnections(): {ex.Message}");
                return DiagnostictsState.StateError;
            }
        }

        /// <summary>
        /// Check and return diagnostics state of the Radea connection
        /// </summary>
        /// <returns></returns>
        public int CheckRadeaConnection()
        {
            try
            {
                var Radea = executor.Radea;

                if (false == Radea.PingServer())
                    return DiagnostictsState.StateError;

                return DiagnostictsState.StateOk; // Radea was connected
            }
            catch (Exception ex)
            {
                logger.Debug($"Got exception in CheckRadeaConnection(): {ex.Message}");
                return DiagnostictsState.StateError;
            }
        }

        public int LookupSize
        {
            get { return (process != null ? process.LookupSize : 0); }
        }

        /// <summary>
        /// Check and return diagnostics state of the application
        /// </summary>
        /// <returns></returns>
        public int CheckApplication()
        {
            try
            {
                // Utilized the logic (..not the most clear) allready implemented in the Process/Gate  
                int state = process.IsOffline ? DiagnostictsState.StateError : DiagnostictsState.StateOk;

                if (state == DiagnostictsState.StateError)
                    selfDg.application.additionalInfoString = ErrorString;
                else
                    selfDg.application.additionalInfoString = "";

                return state;
            }
            catch (Exception ex)
            {
                logger.Debug($"Got exception in CheckApplication(): {ex.Message}");
                return DiagnostictsState.StateError;
            }
        }

        /// <summary>
        /// Do antenna measurements and, optionally, report the results to Radea
        /// </summary>
        /// <param name="performFullAntennaMonitoring">If true, perform full antenna monitoring: measurement + Radea reporting. If false, do only measurements.</param>
        /// <returns></returns>
        public int CheckAntennas(bool performFullAntennaMonitoring)
        {
            int tryCount = TRY_COUNT; // In reporting antenna data to Radea

            try
            {
                // Assume one reader and one area
                Multireader.Reader reader = executor.Multireader.Readers.First();
                Multireader.Area area = executor.Multireader.Areas.First();
                string appName = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title;

                string infoString = $"Starting to check applicaton {appName} reader \"{(reader?.Hostname) ?? "(null)"}\" antennas in area \"{(area?.Name) ?? "(null)"}\"";
                logger.Info(infoString);

                int error_code = 0;
                int dgState = DiagnostictsState.StateOk;
                bool reportingSucceeded = false;

                List<Tuple<int, double, bool, bool>> antennaData = reader.AntennaMonitoring(area.Antennas);
                if (antennaData == null)
                {
                    string errorString = $"AntennaMonitoring returned null value for applicaton {appName} reader \"{(reader?.Hostname) ?? "(null)"}\" antennas in area \"{(area?.Name) ?? "(null)"}\"";
                    throw (new Exception(errorString));
                }

                foreach (Tuple<int, double, bool, bool> singleAntennaData in antennaData)
                {
                    if (singleAntennaData.Item3 && !singleAntennaData.Item4)
                    {
                        error_code = 0x00ee0001;
                        dgState = DiagnostictsState.StateError;
                        break;
                    }
                }

                // Just measure - do not report to Radea
                if (false == performFullAntennaMonitoring)
                    return dgState;

                string locationUuid = process.RouterGateLocationUuid.ToString();
                string readerUuid = process.ReaderUuid;

                while (tryCount-- > 0)
                {
                    reportingSucceeded = executor.Radea.RadeaSyncMonitorReport(antennaData, error_code, appName, locationUuid, readerUuid);

                    if (reportingSucceeded == false)
                    {
                        Task.Delay(WAIT_BETWEEN_RETRY);
                        continue;
                    }
                    else
                        break;
                }

                if (false == reportingSucceeded)
                {
                    string errorString = $"Could not store antenna monitoring data to Radea in applicaton {appName} reader \"{(reader?.Hostname) ?? "(null)"}\"";
                    throw (new Exception(errorString));
                }

                return dgState;
            }
            catch (Exception ex) // Something went wrong with antenna monitoring
            {
                // "Error" instead of "Debug" - this test is run so seldom that we can affort better Log visibility
                logger.Error($"Got exception in CheckAntennas(): {ex.Message}");

                // Do not throw, just issue a warning instead.
                return DiagnostictsState.StateWarning;
            }
        }

        /*************************************
         * Properties for data UI binding
         ************************************/
        public int ReaderDiagnosticsState
        {
            get { return selfDg.reader.currentDiagnosticsState; }
            set
            {
                if (selfDg.reader != null)
                {
                    if (value != selfDg.reader.currentDiagnosticsState)
                    {
                        logger.Info($"ReaderDiagnosticsState {selfDg.reader.ToString()} changed to {DiagnostictsState.ToString(value)}");

                        selfDg.reader.currentDiagnosticsState = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ReaderDiagnosticsState"));
                    }
                }
            }
        }

        public int ApplicationDiagnosticsState
        {
            get { return selfDg.application.currentDiagnosticsState; }
            set
            {
                if (selfDg.application != null)
                {
                    if (value != selfDg.application.currentDiagnosticsState)
                    {
                        logger.Info($"ApplicationDiagnosticsState {selfDg.application.ToString()} changed to {DiagnostictsState.ToString(value)}");

                        selfDg.application.currentDiagnosticsState = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ApplicationDiagnosticsState"));
                    }
                }
            }
        }

        public int RadeaDiagnosticsState
        {
            get { return selfDg.radea.currentDiagnosticsState; }
            set
            {
                if (selfDg.radea != null)
                {
                    if (value != selfDg.radea.currentDiagnosticsState)
                    {
                        logger.Info($"RadeaDiagnosticsState {selfDg.radea.ToString()} changed to {DiagnostictsState.ToString(value)}");

                        selfDg.radea.currentDiagnosticsState = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("RadeaDiagnosticsState"));
                    }
                }
            }
        }

        // Antennas state property for binding
        public int AntennasDiagnosticsState
        {
            get
            {
                return (selfDg.antennas.currentDiagnosticsState);
            }
            set
            {
                if (selfDg?.antennas != null)
                {
                    if (selfDg.antennas.currentDiagnosticsState != value)
                    {
                        logger.Info($"AntennasDiagnosticsState {selfDg.antennas.ToString()} changed to {DiagnostictsState.ToString(value)}");

                        selfDg.antennas.currentDiagnosticsState = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AntennasDiagnosticsState"));
                    }
                }
            }
        }
    }


    /// <summary>
    /// Class representing possible states of a tracked/self-diagnosed item
    /// </summary>
    public class DiagnostictsState
    {
        public const int StateOk = 0x0000;
        public const int StateWarning = 0x0001;
        public const int StateError = 0x0002;
        public const int StateUnknown = 0xFFFF;

        private int state = StateUnknown;
        public int currentDiagnosticsState
        {
            get { return state; }
            set
            {
                if (state != value)
                {
                    state = value;
                }
            }
        }

        public string additionalInfoString { get; set; }

        // c-tor
        public DiagnostictsState()
        {
            currentDiagnosticsState = StateUnknown;
        }

        public static string ToString(int diagnosticsState)
        {
            System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder($"state {diagnosticsState}");

            switch (diagnosticsState)
            {
                case StateOk:
                    stringBuilder.Append(" (OK)");
                    break;
                case StateWarning:
                    stringBuilder.Append(" (Warning)");
                    break;
                case StateError:
                    stringBuilder.Append(" (Error)");
                    break;

                case StateUnknown:
                default:
                    stringBuilder.Append(" (Unknown)");
                    break;
            }

            return (stringBuilder.ToString());
        }

        public override string ToString()
        {
            System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder((ToString(currentDiagnosticsState)));

            if (additionalInfoString != null && additionalInfoString.Length > 0)
                stringBuilder.Append(additionalInfoString);

            return (stringBuilder.ToString());
        }
    } // class DiagnostictsState


    /// <summary>
    /// Class for encapsulating all tracked/self-diagnosed items and controlling polled diagnostics
    /// </summary>
    public class SelfDiagnosticts
    {
        public DiagnostictsState application;
        public DiagnostictsState reader;
        public DiagnostictsState radea;
        public DiagnostictsState antennas;

        private Timer selfDiagnosticsTimer;
        private const int DIAGNOSTICS_INTERVAL = 10;

        private InteractiveInGateViewModel gate;

        /*
         * Antenna monitoring. 
         */
        bool antennaMonitoringOn;
        private DateTime PreviousAntennaMeasurementRound;
        ulong antennaMeasurementRound = 0LU;

        /*
         *  Full antenna monitoring: antenna measurement + reporting measured data to Radea.
         *  Performed in {ANTENNA_FULL_MONITORING_INTERVAL} intervals. 
         *  Value is got from configuration with key "reader_monitor", if defined, othervice
         *  antenna monitoring is not done.
         */
        private double ANTENNA_FULL_MONITORING_INTERVAL;

        /* 
         * Antenna measurement (with or without reporting to Radea) is performed in {ANTENNA_MEASUREMENT_INTERVAL} intervals.
         * The interval value is:
         * - ANTENNA_FULL_MONITORING_INTERVAL/3, if ANTENNA_FULL_MONITORING_INTERVAL >= 30 minutes
         * - ANTENNA_FULL_MONITORING_INTERVAL/2, if ANTENNA_FULL_MONITORING_INTERVAL < 30 minutes
         */
        private double ANTENNA_MEASUREMENT_INTERVAL;

        /*
         * Measured antenna data is not stored to Radea every time. It is reported to Radea 
         * every {ND_RD_TIME_TO_RADEA} time a measurement is done.
         * - if ANTENNA_FULL_MONITORING_INTERVAL >= 30 minutes, every 3rd time [3]
         * - if ANTENNA_FULL_MONITORING_INTERVAL < 30 minutes, every 2nd time [2]
         */
        private ulong ND_RD_TIME_TO_RADEA;


        public SelfDiagnosticts()
        {
        }

        internal void Start(InteractiveInGateViewModel interactiveInGate)
        {
            gate = interactiveInGate;

            application = new DiagnostictsState();
            reader = new DiagnostictsState();
            radea = new DiagnostictsState();
            antennas = new DiagnostictsState();

            // Set antenna monitoring defaults
            SetAntennaMonitoringDefaults();

            // Start timer for diagnosing health of the system. Start ASAP, but wait as long as possible 
            // before next round (to avoid more than 1 polling thread)
            selfDiagnosticsTimer = new Timer(SelfDiagnose, null, TimeSpan.FromSeconds(0), Timeout.InfiniteTimeSpan);
        }

        private void SetAntennaMonitoringDefaults()
        {
            // Use key "reader_monitor" value in configuration file.
            // ANTENNA_FULL_MONITORING_INTERVAL = ((Process.Conveyor)belt.Process()).ReaderMonitor ?? 0.0;
            ANTENNA_FULL_MONITORING_INTERVAL = gate.process.ReaderMonitor ?? 0.0;

            // Turn antenna monitoring off if not set, or set to 0
            if (0.0 == ANTENNA_FULL_MONITORING_INTERVAL)
                antennaMonitoringOn = false;
            else
                antennaMonitoringOn = true;

            // Make first monitoring round to be run on star
            PreviousAntennaMeasurementRound = DateTime.MinValue; // DateTime.Now;

            // ANTENNA_FULL_MONITORING_INTERVAL >= 30 minutes
            if (ANTENNA_FULL_MONITORING_INTERVAL >= 1800)
            {
                ANTENNA_MEASUREMENT_INTERVAL = ANTENNA_FULL_MONITORING_INTERVAL / 3;
                ND_RD_TIME_TO_RADEA = 3LU;
            }
            else // ANTENNA_FULL_MONITORING_INTERVAL < 30 minutes
            {
                ANTENNA_MEASUREMENT_INTERVAL = ANTENNA_FULL_MONITORING_INTERVAL / 2;
                ND_RD_TIME_TO_RADEA = 2LU;
            }
        }

        /// <summary>
        /// Perform one polled diagnostics round for all tracked items
        /// </summary>
        /// <param name="state"></param>
        void SelfDiagnose(object state)
        {
           
            // Check reader
            gate.ReaderDiagnosticsState = gate.CheckReaderConnections();

            // Check Radea
            gate.RadeaDiagnosticsState = gate.CheckRadeaConnection();

            // Check application 
            gate.ApplicationDiagnosticsState = gate.CheckApplication();

            // Check antennas, less frequently
            if (antennaMonitoringOn && TimeToDoAntennaMeasurement())
            {
                antennaMeasurementRound++;

                // Perform full antenna monitorin (measurement + reporting to Radea) even less frequently
                bool performFullAntennaMonitoring = ((antennaMeasurementRound % ND_RD_TIME_TO_RADEA) == 0);
                gate.AntennasDiagnosticsState = gate.CheckAntennas(performFullAntennaMonitoring);

                // Set the base time for next comparison AFTER the antenna measurements have been done
                PreviousAntennaMeasurementRound = DateTime.Now;
            }

            // All done - reset timing to start this method again after DIAGNOSTICS_INTERVAL
            selfDiagnosticsTimer.Change(TimeSpan.FromSeconds(DIAGNOSTICS_INTERVAL), Timeout.InfiniteTimeSpan);
        }

        private bool TimeToDoAntennaMeasurement()
        {
            if (DateTime.Now > (PreviousAntennaMeasurementRound + TimeSpan.FromSeconds(ANTENNA_MEASUREMENT_INTERVAL)))
                return true;
            else
                return false;
        }

        bool DiagnosticsStatesOk()
        {
            return (application.currentDiagnosticsState == DiagnostictsState.StateOk &&
                    reader.currentDiagnosticsState == DiagnostictsState.StateOk &&
                    radea.currentDiagnosticsState == DiagnostictsState.StateOk &&
                    antennas.currentDiagnosticsState == DiagnostictsState.StateOk);
        }

        public override string ToString()
        {
            System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder();
            stringBuilder.Append(application.ToString());
            stringBuilder.Append(reader.ToString());
            stringBuilder.Append(radea.ToString());
            stringBuilder.Append(antennas.ToString());

            return (stringBuilder.ToString());
        }
    } // class SelfDiagnosticts


    public class StateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var number = (int)value;
            var stateColor = "StateUnknown";

            if (number == DiagnostictsState.StateOk)
                stateColor = "StateOk";
            else if (number == DiagnostictsState.StateWarning)
                stateColor = "StateWarning";
            else if (number == DiagnostictsState.StateError)
                stateColor = "StateError";
            else if (number == DiagnostictsState.StateUnknown)
                stateColor = "StateUnknown";
            else
                stateColor = "StateUnknown";

            return (SolidColorBrush)App.Current.FindResource(stateColor);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    public class StateToTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var number = (int)value;
            var toolTip = "State: Unknown";


            if (number == DiagnostictsState.StateOk)
                toolTip = $"State: {number} (OK)";
            else if (number == DiagnostictsState.StateWarning)
                toolTip = $"State: {number} (Warning/potential problem)";
            else if (number == DiagnostictsState.StateError)
                toolTip = $"State: {number} (Error)";
            else if (number == DiagnostictsState.StateUnknown)
                toolTip = $"State: {number} (Unknown)";
            else
                toolTip = $"State: {number} (Unknown, REALLY unknown)";

            return (toolTip);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
