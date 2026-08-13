using System.Collections.Generic;
using UnityEngine;

namespace GameVocal
{
    [System.Serializable]
    public class ARKitMappingEntry
    {
        public string meshPath;
        public string blendshapeName;
        public int blendshapeIndex;
        public bool unavailable;

        public ARKitMappingEntry(string meshPath, string blendshapeName, int blendshapeIndex, bool unavailable)
        {
            this.meshPath = meshPath;
            this.blendshapeName = blendshapeName;
            this.blendshapeIndex = blendshapeIndex;
            this.unavailable = unavailable;
        }
    }

    [System.Serializable]
    public class Viseme2DMappingEntry
    {
        public string key;
        public int frameIndex;
        public Sprite texture;
        public string animationName;
    }

    [CreateAssetMenu(fileName = "NewCharacterProfile", menuName = "GameVocal/Character Profile")]
    public class GameVocalCharacterProfile : ScriptableObject
    {
        // Unity doesn't serialize dictionaries well natively without custom serialization,
        // so we use lists of key-value pairs for the inspector and build dictionaries at runtime.
        
        [System.Serializable]
        public class ARKitPair
        {
            public string key;
            public ARKitMappingEntry value;
        }

        [SerializeField] private List<ARKitPair> arkitMappingList = new List<ARKitPair>();
        [SerializeField] private List<Viseme2DMappingEntry> viseme2DMappingList = new List<Viseme2DMappingEntry>();

        private Dictionary<string, ARKitMappingEntry> _arkitMapping;
        private Dictionary<string, Viseme2DMappingEntry> _viseme2DMapping;

        public Dictionary<string, ARKitMappingEntry> arkitMapping
        {
            get
            {
                if (_arkitMapping == null)
                {
                    _arkitMapping = new Dictionary<string, ARKitMappingEntry>();
                    foreach (var pair in arkitMappingList)
                    {
                        _arkitMapping[pair.key] = pair.value;
                    }
                }
                return _arkitMapping;
            }
        }

        public Dictionary<string, Viseme2DMappingEntry> viseme2dMapping
        {
            get
            {
                if (_viseme2DMapping == null)
                {
                    _viseme2DMapping = new Dictionary<string, Viseme2DMappingEntry>();
                    foreach (var entry in viseme2DMappingList)
                    {
                        _viseme2DMapping[entry.key] = entry;
                    }
                }
                return _viseme2DMapping;
            }
        }

        public ARKitMappingEntry GetMapping(string arkitName)
        {
            if (arkitMapping.TryGetValue(arkitName, out var entry))
                return entry;
            return null;
        }

        public void SetMapping(string arkitName, string meshPath, string blendshapeName, int blendshapeIndex, bool unavailable = false)
        {
            var entry = new ARKitMappingEntry(meshPath, blendshapeName, blendshapeIndex, unavailable);
            arkitMapping[arkitName] = entry;
            
            var existingPair = arkitMappingList.Find(p => p.key == arkitName);
            if (existingPair != null)
            {
                existingPair.value = entry;
            }
            else
            {
                arkitMappingList.Add(new ARKitPair { key = arkitName, value = entry });
            }

            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        public Viseme2DMappingEntry Get2DMapping(string visemeName)
        {
            if (viseme2dMapping.TryGetValue(visemeName, out var entry))
                return entry;
            return null;
        }
        
        public void Set2DMappingFrame(string visemeName, int frameIndex)
        {
            EnsureVisemeEntry(visemeName).frameIndex = frameIndex;
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        public void Set2DMappingTexture(string visemeName, Sprite texture)
        {
            EnsureVisemeEntry(visemeName).texture = texture;
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }

        public void Set2DMappingAnimation(string visemeName, string animationName)
        {
            EnsureVisemeEntry(visemeName).animationName = animationName;
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        
        private Viseme2DMappingEntry EnsureVisemeEntry(string visemeName)
        {
            if (!viseme2dMapping.TryGetValue(visemeName, out var entry))
            {
                entry = new Viseme2DMappingEntry { key = visemeName };
                viseme2dMapping[visemeName] = entry;
                viseme2DMappingList.Add(entry);
            }
            return entry;
        }

        public void ClearArkitMapping()
        {
            arkitMapping.Clear();
            arkitMappingList.Clear();
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
    }
}
