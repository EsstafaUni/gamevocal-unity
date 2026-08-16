using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GameVocal.Editor
{
    public class GameVocalDownloadManager
    {
        public class DownloadItem
        {
            public string url;
            public string logicalPath;
            public string checksum;
            public string absoluteTargetPath;
        }

        private Queue<DownloadItem> _queue = new Queue<DownloadItem>();
        private bool _isDownloading = false;
        private int _totalItems = 0;
        private int _completedItems = 0;

        public event Action<int, int, string> OnProgress; // completed, total, currentFileName
        public event Action<List<DownloadItem>> OnAllDownloadsCompleted; // Passes successful items
        public event Action<string> OnDownloadFailed;

        private List<DownloadItem> _successfulDownloads = new List<DownloadItem>();

        public void QueueDownload(string url, string logicalPath, string checksum)
        {
            _queue.Enqueue(new DownloadItem
            {
                url = url,
                logicalPath = logicalPath,
                checksum = checksum,
                absoluteTargetPath = GameVocalPathUtils.GetAbsolutePath(logicalPath)
            });
        }

        public async void StartQueue()
        {
            if (_isDownloading) return;
            
            _totalItems = _queue.Count;
            _completedItems = 0;
            _successfulDownloads.Clear();
            _isDownloading = true;

            await ProcessQueueAsync();
        }

        private async Task ProcessQueueAsync()
        {
            while (_queue.Count > 0)
            {
                var item = _queue.Dequeue();
                OnProgress?.Invoke(_completedItems, _totalItems, Path.GetFileName(item.logicalPath));

                bool success = await DownloadFileAsync(item);
                if (success)
                {
                    _successfulDownloads.Add(item);
                }
                else
                {
                    // For now, if one fails we just log it and continue.
                    // Could also abort the queue depending on strictness.
                }

                _completedItems++;
            }

            _isDownloading = false;
            OnAllDownloadsCompleted?.Invoke(_successfulDownloads);
        }

        private async Task<bool> DownloadFileAsync(DownloadItem item)
        {
            string tempPath = item.absoluteTargetPath + ".tmp";
            string dir = Path.GetDirectoryName(tempPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (UnityWebRequest www = UnityWebRequest.Get(item.url))
            {
                // Spoof user agent to avoid being blocked by Cloudflare/WAF bot protection
                www.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 GameVocalUnityPlugin/1.0");
                www.SetRequestHeader("X-GameVocal-Engine", "unity");

                // Note: GameVocal presigned URLs usually include query parameters for auth
                // (e.g. AWS S3 X-Amz-Signature). We conditionally add Authorization header if it looks like an API endpoint.
                if (!item.url.Contains("X-Amz-Signature") && !item.url.Contains("Expires=") && !item.url.Contains("sig="))
                {
                    string apiKey = GameVocalSettings.ApiKey;
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        www.SetRequestHeader("Authorization", "Bearer " + apiKey);
                    }
                }
                
                var handler = new DownloadHandlerFile(tempPath);
                handler.removeFileOnAbort = true;
                www.downloadHandler = handler;

                var operation = www.SendWebRequest();
                while (!operation.isDone) await Task.Delay(10);

                if (www.result != UnityWebRequest.Result.Success)
                {
                    OnDownloadFailed?.Invoke($"Failed to download {item.logicalPath}: {www.error}");
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                    return false;
                }
            }

            // Move temp to final location (atomic write)
            try
            {
                if (File.Exists(item.absoluteTargetPath))
                    File.Delete(item.absoluteTargetPath);
                File.Move(tempPath, item.absoluteTargetPath);
                return true;
            }
            catch (Exception ex)
            {
                OnDownloadFailed?.Invoke($"Failed to save {item.logicalPath}: {ex.Message}");
                if (File.Exists(tempPath)) File.Delete(tempPath);
                return false;
            }
        }
    }
}
