using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameVocal
{
    public static class GameVocalLocalizationLoader
    {
        // Maintains the loaded localization strings by node ID
        public static Dictionary<string, string> CurrentTranslations = new Dictionary<string, string>();

        public static bool LoadForLanguage(string langCode)
        {
            string relPath = $"Localization/{langCode}/{langCode}.json";
            string absPath = GameVocalPathUtils.GetAbsolutePath(relPath);
            
            return LoadFile(absPath, langCode);
        }

        public static void LoadAll()
        {
            string baseDir = GameVocalPathUtils.GetAbsolutePath("Localization");
            if (!Directory.Exists(baseDir)) return;

            foreach (var dir in Directory.GetDirectories(baseDir))
            {
                string langCode = new DirectoryInfo(dir).Name;
                LoadForLanguage(langCode);
            }
        }

        public static bool LoadFromPath(string path, string langCode)
        {
            return LoadFile(path, langCode);
        }

        private static bool LoadFile(string path, string langCode)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[GameVocal] Localization file not found: {path}");
                return false;
            }

            try
            {
                string jsonString = File.ReadAllText(path);
                JObject data = JObject.Parse(jsonString);

                CurrentTranslations.Clear();

                foreach (var prop in data.Properties())
                {
                    CurrentTranslations[prop.Name] = prop.Value.ToString();
                }

                Debug.Log($"[GameVocal] Loaded {CurrentTranslations.Count} strings for locale '{langCode}'");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameVocal] Failed to parse localization JSON: {ex.Message}");
                return false;
            }
        }
        
        public static string GetTranslation(string nodeId, string fallback = "")
        {
            if (CurrentTranslations.TryGetValue(nodeId, out string text))
            {
                return text;
            }
            return fallback;
        }
    }
}
