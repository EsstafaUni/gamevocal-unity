using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace GameVocal
{
    [RequireComponent(typeof(AudioSource))]
    public class GameVocalCharacter : MonoBehaviour
    {
        public string characterId = "";
        public GameVocalCharacterProfile profile;
        public AudioSource audioPlayer;
        
        [Header("2D Setup")]
        public SpriteRenderer mouthSprite2D;

        private GameVocalLipsyncPlayer _lipsyncPlayer;
        private bool _isPlayingLipsync = false;

        private void Awake()
        {
            if (audioPlayer == null) audioPlayer = GetComponent<AudioSource>();
            _lipsyncPlayer = new GameVocalLipsyncPlayer();
        }

        public void PlayDialogue(string relativeAudioPath, string relativeLipsyncPath)
        {
            if (audioPlayer == null)
            {
                Debug.LogError($"[GameVocal] Missing audio_player on GameVocalCharacter: {name}");
                return;
            }

            // Load audio clip from resources or via unitywebrequest in a real app, 
            // for simplicity assuming it's loaded by the user or from AssetDatabase if in editor.
            // In runtime Unity, loading arbitrary external ogg files requires UnityWebRequestMultimedia.
            // Since Unity doesn't have an exact equivalent to Godot's `load(audio_path)` for loose runtime files
            // without async coroutines, we expect the audio to be handled appropriately or placed in Resources.
            // For feature parity, we assume the clip is provided or can be loaded synchronously (e.g. Resources).
            
            // Try loading from Resources first (stripping "Assets/Resources/" and extension)
            string resPath = relativeAudioPath.Replace("\\", "/");
            if (resPath.Contains("Resources/"))
            {
                int idx = resPath.IndexOf("Resources/") + 10;
                string noExt = Path.GetFileNameWithoutExtension(resPath);
                string dir = Path.GetDirectoryName(resPath.Substring(idx));
                resPath = string.IsNullOrEmpty(dir) ? noExt : dir + "/" + noExt;
            }
            
            AudioClip clip = Resources.Load<AudioClip>(resPath);
            
            #if UNITY_EDITOR
            if (clip == null)
            {
                // Fallback to AssetDatabase in editor
                string absPath = GameVocalPathUtils.GetAbsolutePath(relativeAudioPath);
                string assetPath = "Assets" + absPath.Replace(Application.dataPath, "").Replace("\\", "/");
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            }
            #endif

            if (clip == null)
            {
                Debug.LogError($"[GameVocal] Failed to load audio stream: {relativeAudioPath}");
                return;
            }

            audioPlayer.clip = clip;

            if (_lipsyncPlayer.LoadLipsyncData(relativeLipsyncPath))
            {
                _isPlayingLipsync = true;
                audioPlayer.Play();
            }
            else
            {
                Debug.LogError($"[GameVocal] Failed to load lip-sync data for: {relativeAudioPath}");
            }
        }

        public void Stop()
        {
            _isPlayingLipsync = false;
            if (audioPlayer != null && audioPlayer.isPlaying)
            {
                audioPlayer.Stop();
            }
            ResetBlendshapes();
            ResetVisemes();
        }

        private void Update()
        {
            if (!_isPlayingLipsync || audioPlayer == null || profile == null)
                return;

            if (audioPlayer.isPlaying)
            {
                float currentTime = audioPlayer.time;

                var blendshapes = _lipsyncPlayer.GetInterpolatedBlendshapes(currentTime);
                if (blendshapes.Count > 0)
                    ApplyBlendshapes(blendshapes);

                var visemes = _lipsyncPlayer.GetInterpolatedVisemes(currentTime);
                if (visemes.Count > 0)
                    ApplyVisemes(visemes);
            }
            else
            {
                _isPlayingLipsync = false;
                ResetBlendshapes();
                ResetVisemes();
            }
        }

        private void ApplyBlendshapes(Dictionary<string, float> blendshapes)
        {
            if (profile == null || profile.arkitMapping.Count == 0) return;

            foreach (var kvp in blendshapes)
            {
                var mapping = profile.GetMapping(kvp.Key);
                if (mapping == null || mapping.unavailable) continue;

                if (string.IsNullOrEmpty(mapping.meshPath) || mapping.blendshapeIndex < 0) continue;

                var meshTransform = transform.Find(mapping.meshPath) ?? GameObject.Find(mapping.meshPath)?.transform;
                if (meshTransform != null)
                {
                    var renderer = meshTransform.GetComponent<SkinnedMeshRenderer>();
                    if (renderer != null && mapping.blendshapeIndex < renderer.sharedMesh.blendShapeCount)
                    {
                        // Unity blendshapes are 0-100 scale, GameVocal/ARKit are 0-1
                        renderer.SetBlendShapeWeight(mapping.blendshapeIndex, kvp.Value * 100f);
                    }
                }
            }
        }

        public void ResetBlendshapes()
        {
            if (profile == null || profile.arkitMapping.Count == 0) return;

            foreach (var arkitName in profile.arkitMapping.Keys)
            {
                var mapping = profile.GetMapping(arkitName);
                if (mapping == null || mapping.unavailable) continue;

                if (string.IsNullOrEmpty(mapping.meshPath) || mapping.blendshapeIndex < 0) continue;

                var meshTransform = transform.Find(mapping.meshPath) ?? GameObject.Find(mapping.meshPath)?.transform;
                if (meshTransform != null)
                {
                    var renderer = meshTransform.GetComponent<SkinnedMeshRenderer>();
                    if (renderer != null && mapping.blendshapeIndex < renderer.sharedMesh.blendShapeCount)
                    {
                        renderer.SetBlendShapeWeight(mapping.blendshapeIndex, 0f);
                    }
                }
            }
        }

        private void ApplyVisemes(Dictionary<string, float> visemes)
        {
            if (profile == null || mouthSprite2D == null) return;

            float maxWeight = -1f;
            string dominantViseme = "";
            
            foreach (var kvp in visemes)
            {
                if (kvp.Value > maxWeight)
                {
                    maxWeight = kvp.Value;
                    dominantViseme = kvp.Key;
                }
            }

            if (string.IsNullOrEmpty(dominantViseme)) return;

            var mapping = profile.Get2DMapping(dominantViseme);
            if (mapping == null) return;

            ApplyVisemeToSprite(mapping);
        }

        private void ResetVisemes()
        {
            if (profile == null || mouthSprite2D == null) return;
            var mapping = profile.Get2DMapping("rest");
            if (mapping != null) ApplyVisemeToSprite(mapping);
        }

        private void ApplyVisemeToSprite(Viseme2DMappingEntry mapping)
        {
            if (mouthSprite2D != null)
            {
                if (mapping.texture != null)
                {
                    mouthSprite2D.sprite = mapping.texture;
                }
                // If using Animator instead of SpriteRenderer, you would set parameters/play states here
                // based on mapping.animationName.
                var animator = mouthSprite2D.GetComponent<Animator>();
                if (animator != null && !string.IsNullOrEmpty(mapping.animationName))
                {
                    animator.Play(mapping.animationName);
                }
            }
        }

        // Diagnostics called from custom editor
        public void ValidateMappingsNow()
        {
            if (profile == null)
            {
                Debug.LogError("[GameVocal] Cannot validate: profile is missing.");
                return;
            }

            int resolved = 0, unavailable = 0, invalid = 0;
            var seenAssignments = new HashSet<string>();

            foreach (var kvp in profile.arkitMapping)
            {
                var arkitName = kvp.Key;
                var mapping = kvp.Value;

                if (mapping.unavailable)
                {
                    unavailable++;
                    continue;
                }

                if (string.IsNullOrEmpty(mapping.meshPath) || mapping.blendshapeIndex < 0)
                {
                    invalid++;
                    Debug.LogError($"[GameVocal] Invalid mapping data for: {arkitName}");
                    continue;
                }

                var meshTransform = transform.Find(mapping.meshPath) ?? GameObject.Find(mapping.meshPath)?.transform;
                var renderer = meshTransform?.GetComponent<SkinnedMeshRenderer>();
                
                if (renderer == null || renderer.sharedMesh == null)
                {
                    Debug.LogError($"[GameVocal] Mesh at path '{mapping.meshPath}' not found for {arkitName}");
                    invalid++;
                    continue;
                }

                if (mapping.blendshapeIndex >= renderer.sharedMesh.blendShapeCount || 
                    renderer.sharedMesh.GetBlendShapeName(mapping.blendshapeIndex) != mapping.blendshapeName)
                {
                    Debug.LogError($"[GameVocal] Blendshape mismatch for {arkitName}. Has the mesh changed?");
                    invalid++;
                    continue;
                }

                string assignmentKey = $"{mapping.meshPath}:{mapping.blendshapeIndex}";
                if (!seenAssignments.Add(assignmentKey))
                {
                    Debug.LogError($"[GameVocal] Duplicate assignment for {arkitName}");
                    invalid++;
                    continue;
                }
                
                resolved++;
            }

            Debug.LogWarning($"[GameVocal] Mapping Validation: {resolved}/{profile.arkitMapping.Count} resolved. {unavailable} intentionally unavailable. {invalid} invalid or conflicting.");
        }

        public void RunDiagnosticTest()
        {
            if (profile == null)
            {
                Debug.LogError("[GameVocal] Cannot run diagnostic: profile is missing.");
                return;
            }

            Debug.LogWarning("[GameVocal] Diagnostic: setting all mapped blendshapes to 100%...");
            ResetBlendshapes();

            foreach (var kvp in profile.arkitMapping)
            {
                if (kvp.Value.unavailable) continue;
                
                var meshTransform = transform.Find(kvp.Value.meshPath) ?? GameObject.Find(kvp.Value.meshPath)?.transform;
                var renderer = meshTransform?.GetComponent<SkinnedMeshRenderer>();
                if (renderer != null && kvp.Value.blendshapeIndex < renderer.sharedMesh.blendShapeCount)
                {
                    renderer.SetBlendShapeWeight(kvp.Value.blendshapeIndex, 100f);
                }
            }
        }
    }
}
