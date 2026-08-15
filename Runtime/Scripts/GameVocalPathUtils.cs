using System.IO;
using UnityEngine;

namespace GameVocal
{
    public static class GameVocalPathUtils
    {
        public static string SanitizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            
            string safePath = path.Replace("\\", "/");
            
            // Trim leading slashes and protocol schemes (like res://)
            if (safePath.StartsWith("res://")) safePath = safePath.Substring(6);
            while (safePath.StartsWith("/")) safePath = safePath.Substring(1);
            
            safePath = safePath.Replace("../", "").Replace("..", "");
            
            return safePath;
        }

        public static string GetImportRoot()
        {
            string projectName = "Project";
#if UNITY_EDITOR
            var type = System.Type.GetType("GameVocal.Editor.GameVocalSettings, GameVocal.Editor");
            if (type != null)
            {
                var prop = type.GetProperty("ActiveProjectName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null)
                {
                    string name = prop.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(name)) projectName = name;
                }
            }
#endif
            
            // Sanitize the project name to prevent invalid path characters
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                projectName = projectName.Replace(c.ToString(), "");
            }
            projectName = projectName.Replace(" ", "_");

            return Path.Combine(Application.dataPath, "GameVocal", projectName).Replace("\\", "/");
        }

        public static string GetAbsolutePath(string relativePath)
        {
            string safeRel = SanitizePath(relativePath);
            return Path.Combine(GetImportRoot(), safeRel).Replace("\\", "/");
        }
    }
}
