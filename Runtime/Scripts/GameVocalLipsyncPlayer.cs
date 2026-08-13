using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameVocal
{
    public class GameVocalLipsyncPlayer
    {
        private List<string> _blendshapeNames = new List<string>();
        private JArray _frames;
        private float _duration = 0f;

        public bool LoadLipsyncData(string relativePath)
        {
            string path = GameVocalPathUtils.GetAbsolutePath(relativePath);
            if (!File.Exists(path))
            {
                Debug.LogError($"[GameVocal] Failed to open lip-sync file: {path}");
                return false;
            }

            try
            {
                string jsonString = File.ReadAllText(path);
                JObject data = JObject.Parse(jsonString);

                if (data["frames"] == null || data["frames"].Type != JTokenType.Array)
                {
                    Debug.LogError($"[GameVocal] Invalid lip-sync JSON format: {path}");
                    return false;
                }

                _blendshapeNames.Clear();
                if (data["blendshape_names"] is JArray arkitNames)
                {
                    foreach (var name in arkitNames) _blendshapeNames.Add(name.ToString());
                }
                else if (data["viseme_names"] is JArray visemeNames)
                {
                    foreach (var name in visemeNames) _blendshapeNames.Add(name.ToString());
                }

                _frames = data["frames"] as JArray;
                _duration = data["duration"]?.Value<float>() ?? 0f;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameVocal] Failed to parse lip-sync JSON: {ex.Message}");
                return false;
            }
        }

        public float GetDuration() => _duration;

        public Dictionary<string, float> GetInterpolatedBlendshapes(float time)
        {
            var result = new Dictionary<string, float>();
            if (_frames == null || _frames.Count == 0) return result;

            int frameIdx = FindFrameIndex(time);
            JObject currentFrame = _frames[frameIdx] as JObject;

            if (frameIdx < _frames.Count - 1)
            {
                JObject nextFrame = _frames[frameIdx + 1] as JObject;
                float ratio = FrameRatio(currentFrame, nextFrame, time);

                if (currentFrame["engine"] is JObject curEngine && nextFrame["engine"] is JObject nxtEngine)
                {
                    foreach (var prop in curEngine.Properties())
                    {
                        float v0 = prop.Value.Value<float>();
                        float v1 = nxtEngine[prop.Name]?.Value<float>() ?? 0f;
                        result[prop.Name] = Mathf.Lerp(v0, v1, ratio);
                    }
                }
                else
                {
                    InterpolateArrayFormat(currentFrame, nextFrame, ratio, result);
                }
            }
            else
            {
                if (currentFrame["engine"] is JObject curEngine)
                {
                    foreach (var prop in curEngine.Properties())
                    {
                        result[prop.Name] = prop.Value.Value<float>();
                    }
                }
                else
                {
                    var shapes = currentFrame["blendshapes"] as JArray;
                    for (int i = 0; i < _blendshapeNames.Count; i++)
                    {
                        result[_blendshapeNames[i]] = (shapes != null && i < shapes.Count) ? shapes[i].Value<float>() : 0f;
                    }
                }
            }

            return result;
        }

        public Dictionary<string, float> GetInterpolatedVisemes(float time)
        {
            var result = new Dictionary<string, float>();
            if (_frames == null || _frames.Count == 0) return result;

            int frameIdx = FindFrameIndex(time);
            JObject currentFrame = _frames[frameIdx] as JObject;

            if (frameIdx < _frames.Count - 1)
            {
                JObject nextFrame = _frames[frameIdx + 1] as JObject;
                float ratio = FrameRatio(currentFrame, nextFrame, time);

                if (currentFrame["viseme_weights"] is JObject curW && nextFrame["viseme_weights"] is JObject nxtW)
                {
                    foreach (var prop in curW.Properties())
                    {
                        float v0 = prop.Value.Value<float>();
                        float v1 = nxtW[prop.Name]?.Value<float>() ?? 0f;
                        result[prop.Name] = Mathf.Lerp(v0, v1, ratio);
                    }
                }
            }
            else
            {
                if (currentFrame["viseme_weights"] is JObject curW)
                {
                    foreach (var prop in curW.Properties())
                    {
                        result[prop.Name] = prop.Value.Value<float>();
                    }
                }
            }

            return result;
        }

        private int FindFrameIndex(float time)
        {
            int idx = 0;
            for (int i = 0; i < _frames.Count; i++)
            {
                if (_frames[i]["time"]?.Value<float>() > time) break;
                idx = i;
            }
            return idx;
        }

        private float FrameRatio(JObject cur, JObject nxt, float time)
        {
            float t0 = cur["time"]?.Value<float>() ?? 0f;
            float t1 = nxt["time"]?.Value<float>() ?? 0f;
            if (t1 <= t0) return 0f;
            return Mathf.Clamp01((time - t0) / (t1 - t0));
        }

        private void InterpolateArrayFormat(JObject cur, JObject nxt, float ratio, Dictionary<string, float> outDict)
        {
            var curShapes = cur["blendshapes"] as JArray;
            var nxtShapes = nxt["blendshapes"] as JArray;

            for (int i = 0; i < _blendshapeNames.Count; i++)
            {
                float v0 = (curShapes != null && i < curShapes.Count) ? curShapes[i].Value<float>() : 0f;
                float v1 = (nxtShapes != null && i < nxtShapes.Count) ? nxtShapes[i].Value<float>() : 0f;
                outDict[_blendshapeNames[i]] = Mathf.Lerp(v0, v1, ratio);
            }
        }
    }
}
