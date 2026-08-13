using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace GameVocal.Editor
{
    public class GameVocalSyncWindow : EditorWindow
    {
        private GameVocalApiClient _apiClient;
        private GameVocalDownloadManager _downloadManager;
        private GameVocalProjectManifest _manifest;

        private List<ProjectData> _projects = new List<ProjectData>();
        private int _selectedProjectIndex = -1;

        private bool _isSyncing = false;
        private string _syncStatusMessage = "";
        private float _syncProgress = 0f;

        // Polling vars
        private bool _isLiveSyncEnabled = false;
        private double _lastPollTime = 0;
        private const double PollIntervalSeconds = 3.0;

        private Vector2 _scrollPos;
        private Texture2D _logoTexture;

        private class ProjectData
        {
            public string id;
            public string name;
            public int version;
        }

        [MenuItem("GameVocal/Sync from GameVocal", false, 1)]
        [MenuItem("Tools/GameVocal/Sync from GameVocal", false, 4000)]
        [MenuItem("Window/GameVocal/Sync from GameVocal", false, 4000)]
        public static void ShowWindow()
        {
            var window = GetWindow<GameVocalSyncWindow>("GameVocal Sync");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void OnEnable()
        {
            _logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.gamevocal.plugin/Editor/icon.png");

            _apiClient = new GameVocalApiClient();
            _apiClient.OnError += msg => Debug.LogError($"[GameVocal] {msg}");

            _downloadManager = new GameVocalDownloadManager();
            _downloadManager.OnProgress += (completed, total, file) =>
            {
                _syncProgress = (float)completed / total;
                _syncStatusMessage = $"Downloading {file} ({completed}/{total})...";
                Repaint();
            };
            _downloadManager.OnAllDownloadsCompleted += OnDownloadsFinished;
            _downloadManager.OnDownloadFailed += msg => Debug.LogError($"[GameVocal] {msg}");

            _manifest = GameVocalProjectManifest.LoadManifest();

            if (!string.IsNullOrEmpty(GameVocalSettings.ApiKey))
            {
                FetchProjects();
            }

            EditorApplication.update += EditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
        }

        private void EditorUpdate()
        {
            if (_isLiveSyncEnabled && !_isSyncing && _selectedProjectIndex >= 0 && _selectedProjectIndex < _projects.Count)
            {
                if (EditorApplication.timeSinceStartup - _lastPollTime > PollIntervalSeconds)
                {
                    _lastPollTime = EditorApplication.timeSinceStartup;
                    PollForChanges();
                }
            }
        }

        private void OnGUI()
        {
            if (_logoTexture != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(_logoTexture, GUILayout.Width(64), GUILayout.Height(64));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            GUILayout.Label("GameVocal Connection", GameVocalEditorStyles.HeaderLabel);

            EditorGUI.BeginChangeCheck();
            string newKey = EditorGUILayout.PasswordField("API Key", GameVocalSettings.ApiKey);
            if (EditorGUI.EndChangeCheck())
            {
                GameVocalSettings.ApiKey = newKey;
            }

            if (GUILayout.Button("Save API Key & Refresh"))
            {
                FetchProjects();
            }

            GUILayout.Space(20);

            if (_projects.Count > 0)
            {
                GUILayout.Label("Cloud Project", GameVocalEditorStyles.HeaderLabel);
                var options = _projects.Select(p => p.name).ToArray();
                
                EditorGUI.BeginChangeCheck();
                _selectedProjectIndex = EditorGUILayout.Popup("Select Project", _selectedProjectIndex, options);
                if (EditorGUI.EndChangeCheck())
                {
                    GameVocalSettings.ActiveProjectId = _projects[_selectedProjectIndex].id;
                }

                GUILayout.Space(10);

                if (_isSyncing)
                {
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), _syncProgress, _syncStatusMessage);
                }
                else
                {
                    var oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = GameVocalEditorStyles.ThemeGreen;
                    if (GUILayout.Button("Sync Assets Now", GUILayout.Height(30)))
                    {
                        StartSync();
                    }
                    GUI.backgroundColor = oldColor;
                    
                    GUILayout.Space(5);
                    _isLiveSyncEnabled = EditorGUILayout.Toggle("Live Dialogue Sync", _isLiveSyncEnabled);
                    if (_isLiveSyncEnabled)
                    {
                        EditorGUILayout.HelpBox("Live Sync is on. Changes in the cloud project will automatically download.", MessageType.Info);
                    }
                }
            }

            GUILayout.Space(20);
            GUILayout.Label("Status", GameVocalEditorStyles.HeaderLabel);
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            if (_manifest.files.Count > 0)
            {
                EditorGUILayout.HelpBox($"Manifest tracks {_manifest.files.Count} files.\nLast sync version: {_manifest.lastSyncVersion}", MessageType.None);
                
                if (GUILayout.Button("Force Clean Sync"))
                {
                    _manifest.DeleteManifest();
                    StartSync();
                }
            }
            else
            {
                GUILayout.Label("No local manifest found.");
            }
            
            EditorGUILayout.EndScrollView();
        }

        private async void FetchProjects()
        {
            var response = await _apiClient.RequestArrayAsync("/projects");
            if (response == null) return;

            _projects.Clear();
            foreach (JObject p in response)
            {
                _projects.Add(new ProjectData
                {
                    id = p["id"]?.ToString(),
                    name = p["name"]?.ToString(),
                    version = p["version"]?.Value<int>() ?? 0
                });
            }

            string savedId = GameVocalSettings.ActiveProjectId;
            _selectedProjectIndex = _projects.FindIndex(p => p.id == savedId);
            if (_selectedProjectIndex == -1 && _projects.Count > 0)
            {
                _selectedProjectIndex = 0;
                GameVocalSettings.ActiveProjectId = _projects[0].id;
            }

            Repaint();
        }

        private async void PollForChanges()
        {
            string projId = _projects[_selectedProjectIndex].id;
            var response = await _apiClient.RequestAsync($"/projects/{projId}/sync-status");
            if (response != null)
            {
                int remoteVersion = response["version"]?.Value<int>() ?? 0;
                if (remoteVersion > _manifest.lastSyncVersion)
                {
                    Debug.Log($"[GameVocal] Cloud version {remoteVersion} > local version {_manifest.lastSyncVersion}. Auto-syncing...");
                    StartSync();
                }
            }
        }

        private async void StartSync()
        {
            if (_selectedProjectIndex < 0 || _selectedProjectIndex >= _projects.Count) return;
            
            _isSyncing = true;
            _syncProgress = 0f;
            _syncStatusMessage = "Fetching manifest...";
            Repaint();

            string projId = _projects[_selectedProjectIndex].id;
            var response = await _apiClient.RequestAsync($"/projects/{projId}/manifest");
            
            if (response == null)
            {
                _isSyncing = false;
                Repaint();
                return;
            }

            int remoteVersion = response["version"]?.Value<int>() ?? 0;
            JArray files = response["files"] as JArray;

            if (files == null || files.Count == 0)
            {
                _isSyncing = false;
                Debug.LogWarning("[GameVocal] Empty manifest received.");
                Repaint();
                return;
            }

            int queuedCount = 0;
            foreach (JObject fileObj in files)
            {
                string logicalPath = fileObj["logical_path"]?.ToString();
                string checksum = fileObj["checksum"]?.ToString();
                string url = fileObj["url"]?.ToString();

                bool needsDownload = true;
                if (_manifest.files.TryGetValue(logicalPath, out string localHash))
                {
                    // Check if file exists on disk
                    string absPath = GameVocalPathUtils.GetAbsolutePath(logicalPath);
                    if (System.IO.File.Exists(absPath) && localHash == checksum)
                    {
                        needsDownload = false;
                    }
                }

                if (needsDownload)
                {
                    _downloadManager.QueueDownload(url, logicalPath, checksum);
                    queuedCount++;
                }
            }

            if (queuedCount == 0)
            {
                _syncStatusMessage = "All files are up to date.";
                _manifest.lastSyncVersion = remoteVersion;
                _manifest.SaveManifest();
                _isSyncing = false;
            }
            else
            {
                _syncStatusMessage = $"Queued {queuedCount} files for download...";
                _manifest.projectId = projId;
                _manifest.lastSyncVersion = remoteVersion;
                _manifest.lastSync = System.DateTime.UtcNow.ToString("O");
                _downloadManager.StartQueue();
            }
            
            Repaint();
        }

        private void OnDownloadsFinished(List<GameVocalDownloadManager.DownloadItem> successfulItems)
        {
            _isSyncing = false;
            _syncProgress = 1f;
            _syncStatusMessage = "Sync complete.";
            
            foreach (var item in successfulItems)
            {
                _manifest.files[item.logicalPath] = item.checksum;
            }
            _manifest.SaveManifest();

            // Refresh AssetDatabase to import newly downloaded files
            AssetDatabase.Refresh();

            Repaint();
        }
    }
}
