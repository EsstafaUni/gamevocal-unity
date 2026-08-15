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
            // Mirroring Godot's "res://", we'll put synced assets into Assets/GameVocalData or similar
            // For now, let's use the StreamingAssets folder or a custom GameVocal folder
            // In the Editor sync, we used Application.dataPath + "/GameVocal/"
            return Path.Combine(Application.dataPath, "GameVocal").Replace("\\", "/");
        }

        public static string GetAbsolutePath(string relativePath)
        {
            string safeRel = SanitizePath(relativePath);
            return Path.Combine(GetImportRoot(), safeRel).Replace("\\", "/");
        }
    }
}
