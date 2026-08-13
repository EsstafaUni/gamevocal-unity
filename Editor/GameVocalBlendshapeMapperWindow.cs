using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameVocal.Editor
{
    public class GameVocalBlendshapeMapperWindow : EditorWindow
    {
        private GameVocalCharacter _targetCharacter;
        private Vector2 _scrollPos;
        private List<SkinnedMeshRenderer> _availableMeshes = new List<SkinnedMeshRenderer>();

        // We use string arrays for popup menus
        private string[] _meshOptions;
        private Dictionary<SkinnedMeshRenderer, string[]> _meshBlendshapeNames = new Dictionary<SkinnedMeshRenderer, string[]>();

        // Preview state
        private string _activePreviewChannel = "";
        private float _originalPreviewWeight = 0f;

        private static readonly string[] ARKit52Channels = new string[]
        {
            "eyeBlinkLeft", "eyeLookDownLeft", "eyeLookInLeft", "eyeLookOutLeft", "eyeLookUpLeft", "eyeSquintLeft", "eyeWideLeft",
            "eyeBlinkRight", "eyeLookDownRight", "eyeLookInRight", "eyeLookOutRight", "eyeLookUpRight", "eyeSquintRight", "eyeWideRight",
            "jawForward", "jawLeft", "jawRight", "jawOpen",
            "mouthClose", "mouthFunnel", "mouthPucker", "mouthLeft", "mouthRight", "mouthSmileLeft", "mouthSmileRight",
            "mouthFrownLeft", "mouthFrownRight", "mouthDimpleLeft", "mouthDimpleRight", "mouthStretchLeft", "mouthStretchRight",
            "mouthRollLower", "mouthRollUpper", "mouthShrugLower", "mouthShrugUpper", "mouthPressLeft", "mouthPressRight",
            "mouthLowerDownLeft", "mouthLowerDownRight", "mouthUpperUpLeft", "mouthUpperUpRight",
            "browDownLeft", "browDownRight", "browInnerUp", "browOuterUpLeft", "browOuterUpRight",
            "cheekPuff", "cheekSquintLeft", "cheekSquintRight",
            "noseSneerLeft", "noseSneerRight",
            "tongueOut"
        };

        public static void ShowWindow(GameVocalCharacter character)
        {
            var window = GetWindow<GameVocalBlendshapeMapperWindow>("ARKit52 Mapper");
            window.minSize = new Vector2(500, 600);
            window._targetCharacter = character;
            window.ScanMeshes();
            window.Show();
        }

        private void ScanMeshes()
        {
            if (_targetCharacter == null) return;

            _availableMeshes.Clear();
            _meshBlendshapeNames.Clear();

            var meshes = _targetCharacter.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in meshes)
            {
                if (smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
                {
                    _availableMeshes.Add(smr);
                    
                    var names = new string[smr.sharedMesh.blendShapeCount + 1];
                    names[0] = "None"; // 0 index means no selection
                    for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                    {
                        names[i + 1] = smr.sharedMesh.GetBlendShapeName(i);
                    }
                    _meshBlendshapeNames[smr] = names;
                }
            }

            var opts = new List<string> { "None" };
            opts.AddRange(_availableMeshes.Select(m => m.name));
            _meshOptions = opts.ToArray();
        }

        private void OnGUI()
        {
            if (_targetCharacter == null || _targetCharacter.profile == null)
            {
                EditorGUILayout.HelpBox("Target character or profile is missing.", MessageType.Error);
                return;
            }

            GUILayout.Space(10);
            GUILayout.Label($"Mapping ARKit52 for: {_targetCharacter.name}", GameVocalEditorStyles.HeaderLabel);
            
            if (_availableMeshes.Count == 0)
            {
                EditorGUILayout.HelpBox("No SkinnedMeshRenderers with blendshapes found under this character.", MessageType.Warning);
                if (GUILayout.Button("Rescan Meshes")) ScanMeshes();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto-Suggest Mappings", GUILayout.Height(30)))
            {
                AutoSuggestMappings();
            }
            if (GUILayout.Button("Clear All", GUILayout.Height(30)))
            {
                _targetCharacter.profile.ClearArkitMapping();
                _activePreviewChannel = "";
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (string channel in ARKit52Channels)
            {
                DrawChannelRow(channel);
            }

            EditorGUILayout.EndScrollView();
            
            GUILayout.Space(10);
            if (GUILayout.Button("Save Profile", GUILayout.Height(40)))
            {
                EditorUtility.SetDirty(_targetCharacter.profile);
                AssetDatabase.SaveAssets();
                Debug.Log("[GameVocal] Character profile saved.");
            }
            GUILayout.Space(10);
        }

        private void DrawChannelRow(string arkitName)
        {
            var mapping = _targetCharacter.profile.GetMapping(arkitName);
            bool isUnavailable = mapping != null && mapping.unavailable;

            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            
            GUILayout.Label(arkitName, GUILayout.Width(130));

            bool newUnavailable = GUILayout.Toggle(isUnavailable, "Skip", GUILayout.Width(50));
            if (newUnavailable != isUnavailable)
            {
                if (newUnavailable)
                {
                    _targetCharacter.profile.SetMapping(arkitName, "", "", -1, true);
                }
                else
                {
                    _targetCharacter.profile.SetMapping(arkitName, "", "", -1, false);
                }
                mapping = _targetCharacter.profile.GetMapping(arkitName);
            }

            GUI.enabled = !newUnavailable;

            // Mesh Dropdown
            int currentMeshIdx = 0;
            SkinnedMeshRenderer currentMesh = null;
            if (mapping != null && !string.IsNullOrEmpty(mapping.meshPath))
            {
                // Find the mesh that matches the path
                for (int i = 0; i < _availableMeshes.Count; i++)
                {
                    if (GetRelativePath(_targetCharacter.transform, _availableMeshes[i].transform) == mapping.meshPath)
                    {
                        currentMeshIdx = i + 1;
                        currentMesh = _availableMeshes[i];
                        break;
                    }
                }
            }

            int newMeshIdx = EditorGUILayout.Popup(currentMeshIdx, _meshOptions, GUILayout.Width(120));
            if (newMeshIdx != currentMeshIdx)
            {
                string newPath = "";
                if (newMeshIdx > 0)
                {
                    currentMesh = _availableMeshes[newMeshIdx - 1];
                    newPath = GetRelativePath(_targetCharacter.transform, currentMesh.transform);
                }
                else
                {
                    currentMesh = null;
                }
                _targetCharacter.profile.SetMapping(arkitName, newPath, "", -1, false);
                mapping = _targetCharacter.profile.GetMapping(arkitName);
            }

            // Blendshape Dropdown
            int currentBsIdx = 0;
            if (currentMesh != null && mapping != null && mapping.blendshapeIndex >= 0)
            {
                // Our stored index is exactly the mesh's index, but the dropdown is +1 shifted due to "None"
                currentBsIdx = mapping.blendshapeIndex + 1;
            }

            string[] bsOptions = currentMesh != null ? _meshBlendshapeNames[currentMesh] : new string[] { "None" };
            
            int newBsIdx = EditorGUILayout.Popup(currentBsIdx, bsOptions, GUILayout.Width(150));
            if (newBsIdx != currentBsIdx)
            {
                if (newBsIdx > 0 && currentMesh != null)
                {
                    int actualIndex = newBsIdx - 1;
                    string bsName = bsOptions[newBsIdx];
                    _targetCharacter.profile.SetMapping(arkitName, GetRelativePath(_targetCharacter.transform, currentMesh.transform), bsName, actualIndex, false);
                }
                else
                {
                    _targetCharacter.profile.SetMapping(arkitName, mapping.meshPath, "", -1, false);
                }
                mapping = _targetCharacter.profile.GetMapping(arkitName);
            }

            GUI.enabled = true;

            // Preview Toggle
            bool isPreviewing = _activePreviewChannel == arkitName;
            bool wantsPreview = GUILayout.Toggle(isPreviewing, "Test (1.0)", "Button", GUILayout.Width(80));

            if (wantsPreview != isPreviewing)
            {
                if (wantsPreview)
                {
                    // Clear previous
                    ClearPreview();
                    
                    // Setup new preview
                    if (currentMesh != null && newBsIdx > 0)
                    {
                        int actualIndex = newBsIdx - 1;
                        _originalPreviewWeight = currentMesh.GetBlendShapeWeight(actualIndex);
                        currentMesh.SetBlendShapeWeight(actualIndex, 100f);
                        _activePreviewChannel = arkitName;
                    }
                }
                else
                {
                    ClearPreview();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ClearPreview()
        {
            if (string.IsNullOrEmpty(_activePreviewChannel)) return;

            var mapping = _targetCharacter.profile.GetMapping(_activePreviewChannel);
            if (mapping != null && !string.IsNullOrEmpty(mapping.meshPath) && mapping.blendshapeIndex >= 0)
            {
                var meshTransform = _targetCharacter.transform.Find(mapping.meshPath);
                if (meshTransform != null)
                {
                    var renderer = meshTransform.GetComponent<SkinnedMeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.SetBlendShapeWeight(mapping.blendshapeIndex, _originalPreviewWeight);
                    }
                }
            }
            _activePreviewChannel = "";
        }

        private void OnDestroy()
        {
            ClearPreview();
        }

        private void AutoSuggestMappings()
        {
            if (_availableMeshes.Count == 0) return;
            
            // For simplicity, we just use the first available mesh that has blendshapes for auto-mapping.
            // A more complex heuristic could scan all meshes for the best fuzzy matches.
            var targetMesh = _availableMeshes[0];
            string meshPath = GetRelativePath(_targetCharacter.transform, targetMesh.transform);

            int mappedCount = 0;
            var meshBsNames = _meshBlendshapeNames[targetMesh];

            foreach (string arkitName in ARKit52Channels)
            {
                var mapping = _targetCharacter.profile.GetMapping(arkitName);
                if (mapping != null && (mapping.unavailable || mapping.blendshapeIndex >= 0)) continue; // Skip if already manually mapped or disabled

                int bestIdx = FindBestMatchIndex(arkitName, meshBsNames);
                if (bestIdx > 0)
                {
                    int actualIndex = bestIdx - 1;
                    _targetCharacter.profile.SetMapping(arkitName, meshPath, meshBsNames[bestIdx], actualIndex, false);
                    mappedCount++;
                }
            }

            Debug.Log($"[GameVocal] Auto-mapped {mappedCount} channels.");
        }

        private int FindBestMatchIndex(string arkitName, string[] options)
        {
            string lowerArkit = arkitName.ToLowerInvariant();
            for (int i = 1; i < options.Length; i++)
            {
                string opt = options[i].ToLowerInvariant();
                // Strip common prefixes (like vrc.v_ or similar if necessary, though ARKit implies exact or close match)
                if (opt.Contains(lowerArkit) || lowerArkit.Contains(opt))
                {
                    return i;
                }
            }
            return -1; // Not found
        }

        private string GetRelativePath(Transform root, Transform target)
        {
            if (root == target) return "";
            string path = target.name;
            Transform curr = target.parent;
            while (curr != null && curr != root)
            {
                path = curr.name + "/" + path;
                curr = curr.parent;
            }
            return path;
        }
    }
}
