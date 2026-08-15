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
            // Mirroring Godot's project-specific folders
            string projectName = string.IsNullOrEmpty(Editor.GameVocalSettings.ActiveProjectName) 
                ? "Project" 
                : Editor.GameVocalSettings.ActiveProjectName;
            
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
