using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameVocal.Editor
{
    public class GameVocalProjectManifest
    {
        public string projectId = "";
        public string lastSync = "";
        public int lastSyncVersion = 0;
        public Dictionary<string, string> files = new Dictionary<string, string>();

        private static string GetManifestPath()
        {
            // Outside of Assets/ so it's not tracked by VCS (like Godot's user://)
            return Path.Combine(Application.dataPath, "../Library/GameVocalManifest.json");
        }

        public static GameVocalProjectManifest LoadManifest()
        {
            string path = GetManifestPath();
            var manifest = new GameVocalProjectManifest();

            if (!File.Exists(path)) return manifest;

            try
            {
                string json = File.ReadAllText(path);
                JObject data = JObject.Parse(json);

                manifest.projectId = data["project_id"]?.ToString() ?? "";
                manifest.lastSync = data["last_sync"]?.ToString() ?? "";
                manifest.lastSyncVersion = data["last_sync_version"]?.Value<int>() ?? 0;

                if (data["files"] is JObject filesObj)
                {
                    foreach (var prop in filesObj.Properties())
                    {
                        manifest.files[prop.Name] = prop.Value.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameVocal] Error loading manifest: {ex.Message}");
            }

            return manifest;
        }

        public void SaveManifest()
        {
            string path = GetManifestPath();
            try
            {
                var filesObj = new JObject();
                foreach (var kvp in files)
                {
                    filesObj[kvp.Key] = kvp.Value;
                }

                var data = new JObject
                {
                    ["project_id"] = projectId,
                    ["last_sync"] = lastSync,
                    ["last_sync_version"] = lastSyncVersion,
                    ["files"] = filesObj
                };

                File.WriteAllText(path, data.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameVocal] Error saving manifest: {ex.Message}");
            }
        }

        public void DeleteManifest()
        {
            string path = GetManifestPath();
            if (File.Exists(path)) File.Delete(path);
            files.Clear();
            projectId = "";
            lastSync = "";
            lastSyncVersion = 0;
        }
    }
}
