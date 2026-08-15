using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace GameVocal.Editor
{
    public class GameVocalSyncDialog : EditorWindow
    {
        public class SyncItem
        {
            public bool isSelected;
            public string logicalPath;
            public string checksum;
            public string url;
            public string statusText;
            public Color statusColor;
        }

        private List<SyncItem> _items = new List<SyncItem>();
        private Action<List<SyncItem>, string, int> _onConfirm;
        private Vector2 _scrollPos;
        
        private string _projectId;
        private int _remoteVersion;

        public static void ShowDialog(JArray files, GameVocalProjectManifest localManifest, string projectId, int remoteVersion, Action<List<SyncItem>, string, int> onConfirm)
        {
            var window = GetWindow<GameVocalSyncDialog>(true, "Select Assets to Sync", true);
            window.minSize = new Vector2(550, 400);
            window.Initialize(files, localManifest, projectId, remoteVersion, onConfirm);
            window.ShowUtility();
        }

        private void Initialize(JArray files, GameVocalProjectManifest localManifest, string projectId, int remoteVersion, Action<List<SyncItem>, string, int> onConfirm)
        {
            _onConfirm = onConfirm;
            _projectId = projectId;
            _remoteVersion = remoteVersion;
            _items.Clear();

            if (files != null)
            {
                foreach (JObject fileObj in files)
                {
                    string logicalPath = fileObj["logical_path"]?.ToString();
                    string checksum = fileObj["checksum"]?.ToString();
                    string url = fileObj["url"]?.ToString();

                    if (string.IsNullOrEmpty(logicalPath) || string.IsNullOrEmpty(url)) continue;

                    bool needsDownload = true;
                    string statusText = "Missing";
                    Color statusColor = new Color(0.9f, 0.3f, 0.3f);

                    if (localManifest.files.TryGetValue(logicalPath, out string localHash))
                    {
                        string absPath = GameVocalPathUtils.GetAbsolutePath(logicalPath);
                        if (File.Exists(absPath))
                        {
                            if (localHash == checksum)
                            {
                                needsDownload = false;
                                statusText = "Up to date";
                                statusColor = new Color(0.4f, 0.8f, 0.4f);
                            }
                            else
                            {
                                statusText = "Out of date";
                                statusColor = new Color(0.8f, 0.6f, 0.2f);
                            }
                        }
                    }

                    _items.Add(new SyncItem
                    {
                        isSelected = needsDownload, // Default to true if missing/out of date
                        logicalPath = logicalPath,
                        checksum = checksum,
                        url = url,
                        statusText = statusText,
                        statusColor = statusColor
                    });
                }
            }
            
            // Sort so missing/out of date are at the top
            _items = _items.OrderByDescending(i => i.isSelected).ThenBy(i => i.logicalPath).ToList();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            
            // Toolbar
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", GUILayout.Width(100)))
            {
                foreach (var item in _items) item.isSelected = true;
            }
            if (GUILayout.Button("Select None", GUILayout.Width(100)))
            {
                foreach (var item in _items) item.isSelected = false;
            }
            GUILayout.FlexibleSpace();
            int selectedCount = _items.Count(i => i.isSelected);
            GUILayout.Label($"{selectedCount} / {_items.Count} Selected");
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Header row
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Sync", GUILayout.Width(40));
            GUILayout.Label("Asset Path", GUILayout.ExpandWidth(true));
            GUILayout.Label("Status", GUILayout.Width(100));
            GUILayout.EndHorizontal();

            // Items List
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, true);
            foreach (var item in _items)
            {
                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                item.isSelected = EditorGUILayout.Toggle(item.isSelected, GUILayout.Width(35));
                GUILayout.Label(item.logicalPath, GUILayout.ExpandWidth(true));
                
                var oldColor = GUI.contentColor;
                GUI.contentColor = item.statusColor;
                GUILayout.Label(item.statusText, EditorStyles.boldLabel, GUILayout.Width(100));
                GUI.contentColor = oldColor;
                
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Space(10);

            // Bottom Buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                Close();
            }
            
            EditorGUI.BeginDisabledGroup(selectedCount == 0);
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = GameVocalEditorStyles.ThemeGreen;
            if (GUILayout.Button($"Sync {selectedCount} Assets", GameVocalEditorStyles.PrimaryButton, GUILayout.Height(30)))
            {
                var selectedItems = _items.Where(i => i.isSelected).ToList();
                _onConfirm?.Invoke(selectedItems, _projectId, _remoteVersion);
                Close();
            }
            GUI.backgroundColor = oldBg;
            EditorGUI.EndDisabledGroup();
            
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }
    }
}
