using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InteractiveInGate.Models
{
    class RuntimeConfig
    {
        public static Dictionary<string, bool> OptionSelected = new Dictionary<string, bool>();
        private static readonly string RUNTIME_SETTINGS_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nordic ID", "InteractiveInGate");
        private static readonly string RUNTIME_SETTINGS_FILE = Path.Combine(
            RUNTIME_SETTINGS_PATH, "RuntimeConfig.json");
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static void StoreRuntimeSettings()
        {
            try
            {
                if (!Directory.Exists(RUNTIME_SETTINGS_PATH))
                {
                    Directory.CreateDirectory(RUNTIME_SETTINGS_PATH);
                }

                string storablePayload = JsonConvert.SerializeObject(OptionSelected);
                File.WriteAllText(RUNTIME_SETTINGS_FILE, storablePayload);
                logger.Info($"Runtime settings saved to disk ({Path.GetFullPath(RUNTIME_SETTINGS_FILE)})");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error storing runtime settings ({Path.GetFullPath(RUNTIME_SETTINGS_FILE)})");
            }
        }

        public static void LoadRuntimeSettings()
        {
            OptionSelected.Clear();

            try
            {
                logger.Info($"Loading runtime settings ({Path.GetFullPath(RUNTIME_SETTINGS_FILE)})");
                if (File.Exists(RUNTIME_SETTINGS_FILE))
                {
                    string storageFileContent = File.ReadAllText(RUNTIME_SETTINGS_FILE);
                    OptionSelected = JsonConvert.DeserializeObject<Dictionary<string, bool>>(storageFileContent);
                }
                else
                {
                    logger.Info("No stored runtime settings found, using factory defaults.");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error loading runtime settings ({Path.GetFullPath(RUNTIME_SETTINGS_FILE)})");
            }
        }
    }
}
