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
        private string _errorMessage = "";
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
            _apiClient.OnError += msg => {
                Debug.LogError($"[GameVocal] {msg}");
                _errorMessage = msg;
                Repaint();
            };

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
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Space(15);

            // --- HEADER ---
            if (_logoTexture != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(_logoTexture, GUILayout.Width(80), GUILayout.Height(80));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }

            // --- AUTHENTICATION SECTION ---
            GUILayout.BeginVertical(GameVocalEditorStyles.SectionBox);
            GUILayout.Label("Authentication", GameVocalEditorStyles.TitleLabel);
            GUILayout.Label("Enter your GameVocal API key to connect your studio.", GameVocalEditorStyles.SubtitleLabel);

            EditorGUI.BeginChangeCheck();
            string newKey = EditorGUILayout.PasswordField("API Key", GameVocalSettings.ApiKey);
            if (EditorGUI.EndChangeCheck())
            {
                GameVocalSettings.ApiKey = newKey;
                _errorMessage = "";
            }
            
            GUILayout.Space(5);
            EditorGUI.BeginChangeCheck();
            string newUrl = EditorGUILayout.TextField("API URL (Dev)", GameVocalSettings.ApiUrl);
            if (EditorGUI.EndChangeCheck())
            {
                GameVocalSettings.ApiUrl = newUrl;
            }

            if (!string.IsNullOrEmpty(_errorMessage))
            {
                GUILayout.Space(5);
                EditorGUILayout.HelpBox(_errorMessage, MessageType.Error);
            }

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save & Refresh", GUILayout.Width(140), GUILayout.Height(25)))
            {
                FetchProjects();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // --- PROJECT SETTINGS SECTION ---
            if (_projects.Count > 0)
            {
                GUILayout.BeginVertical(GameVocalEditorStyles.SectionBox);
                GUILayout.Label("Project Settings", GameVocalEditorStyles.TitleLabel);
                GUILayout.Label("Select the cloud project you want to sync with this Unity project.", GameVocalEditorStyles.SubtitleLabel);

                var options = _projects.Select(p => p.name).ToArray();
                
                EditorGUI.BeginChangeCheck();
                _selectedProjectIndex = EditorGUILayout.Popup("Cloud Project", _selectedProjectIndex, options);
                if (EditorGUI.EndChangeCheck())
                {
                    GameVocalSettings.ActiveProjectId = _projects[_selectedProjectIndex].id;
                    GameVocalSettings.ActiveProjectName = _projects[_selectedProjectIndex].name;
                }
                GUILayout.EndVertical();

                // --- SYNC OPERATIONS SECTION ---
                GUILayout.BeginVertical(GameVocalEditorStyles.SectionBox);
                GUILayout.Label("Sync Operations", GameVocalEditorStyles.TitleLabel);
                GUILayout.Label("Download the latest dialogue and audio assets from GameVocal.", GameVocalEditorStyles.SubtitleLabel);

                GUILayout.Space(10);

                if (_isSyncing)
                {
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(24)), _syncProgress, _syncStatusMessage);
                    GUILayout.Space(10);
                }
                else
                {
                    var oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = GameVocalEditorStyles.ThemeGreen;
                    if (GUILayout.Button("Sync Assets Now", GameVocalEditorStyles.PrimaryButton, GUILayout.Height(36)))
                    {
                        StartSync();
                    }
                    GUI.backgroundColor = oldColor;
                    
                    GUILayout.Space(10);
                    EditorGUILayout.BeginHorizontal();
                    _isLiveSyncEnabled = EditorGUILayout.Toggle(GUIContent.none, _isLiveSyncEnabled, GUILayout.Width(16));
                    GUILayout.Label("Enable Live Dialogue Sync", GUILayout.ExpandWidth(true));
                    EditorGUILayout.EndHorizontal();

                    if (_isLiveSyncEnabled)
                    {
                        EditorGUILayout.HelpBox("Live Sync is active. The plugin will poll the cloud project every few seconds and automatically download changes.", MessageType.Info);
                    }
                }
                GUILayout.EndVertical();
            }

            // --- STATUS & MANIFEST SECTION ---
            GUILayout.BeginVertical(GameVocalEditorStyles.SectionBox);
            GUILayout.Label("System Status", GameVocalEditorStyles.TitleLabel);
            
            if (_manifest.files.Count > 0)
            {
                GUILayout.Label($"Tracking {_manifest.files.Count} files. (Version: {_manifest.lastSyncVersion})", GameVocalEditorStyles.SubtitleLabel);
                
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Force Clean Sync", GameVocalEditorStyles.DangerButton, GUILayout.Width(130)))
                {
                    if (EditorUtility.DisplayDialog("Force Clean Sync", "Are you sure you want to clear the local manifest and force a complete re-download of all assets?", "Yes, Clean Sync", "Cancel"))
                    {
                        _manifest.DeleteManifest();
                        StartSync();
                    }
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("No local manifest found. Run a sync to initialize.", GameVocalEditorStyles.SubtitleLabel);
            }
            GUILayout.EndVertical();
            
            GUILayout.Space(15);
            EditorGUILayout.EndScrollView();
        }

        private async void FetchProjects()
        {
            _errorMessage = "";
            var response = await _apiClient.RequestArrayAsync("/projects/");
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
                GameVocalSettings.ActiveProjectName = _projects[0].name;
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
            
            _isSyncing = false;
            Repaint();

            if (response == null) return;

            int remoteVersion = response["version"]?.Value<int>() ?? 0;
            JArray files = response["files"] as JArray;

            if (files == null || files.Count == 0)
            {
                Debug.LogWarning("[GameVocal] Empty manifest received.");
                return;
            }

            // Launch the Sync Selection Dialog
            GameVocalSyncDialog.ShowDialog(files, _manifest, projId, remoteVersion, ExecuteSync);
        }

        private void ExecuteSync(List<GameVocalSyncDialog.SyncItem> itemsToSync, string projectId, int remoteVersion)
        {
            if (itemsToSync == null || itemsToSync.Count == 0)
            {
                _syncStatusMessage = "No files selected for sync.";
                Repaint();
                return;
            }

            _isSyncing = true;
            _syncProgress = 0f;
            _syncStatusMessage = $"Queued {itemsToSync.Count} files for download...";
            
            foreach (var item in itemsToSync)
            {
                _downloadManager.QueueDownload(item.url, item.logicalPath, item.checksum);
            }

            _manifest.projectId = projectId;
            _manifest.lastSyncVersion = remoteVersion;
            _manifest.lastSync = System.DateTime.UtcNow.ToString("O");
            
            _downloadManager.StartQueue();
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
