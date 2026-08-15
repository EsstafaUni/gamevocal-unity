using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace GameVocal.Editor
{
    public static class GameVocalSettings
    {
        private static string SettingsPath => Path.Combine(Application.dataPath, "../UserSettings/GameVocalSettings.json");

        private static JObject LoadSettings()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    return JObject.Parse(File.ReadAllText(SettingsPath));
                }
                catch
                {
                    return new JObject();
                }
            }
            return new JObject();
        }

        private static void SaveSettings(JObject settings)
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(SettingsPath, settings.ToString());
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameVocal] Failed to save settings: {ex.Message}");
            }
        }

        public static string ApiKey
        {
            get
            {
                var settings = LoadSettings();
                return settings["ApiKey"]?.ToString() ?? "";
            }
            set
            {
                var settings = LoadSettings();
                settings["ApiKey"] = value != null ? value.Trim() : "";
                SaveSettings(settings);
            }
        }

        public static string ActiveProjectId
        {
            get
            {
                var settings = LoadSettings();
                return settings["ActiveProjectId"]?.ToString() ?? "";
            }
            set
            {
                var settings = LoadSettings();
                settings["ActiveProjectId"] = value;
                SaveSettings(settings);
            }
        }

        public static string ActiveProjectName
        {
            get
            {
                var settings = LoadSettings();
                return settings["ActiveProjectName"]?.ToString() ?? "Project";
            }
            set
            {
                var settings = LoadSettings();
                settings["ActiveProjectName"] = value;
                SaveSettings(settings);
            }
        }
    }
}
