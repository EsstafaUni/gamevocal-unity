using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameVocal
{
    [System.Serializable]
    public class GameVocalDialogueLine
    {
        public string id = "";
        public string type = "";
        public string text = "";
        public string character_id = "";
        public string line_type = "voiced";

        public Dictionary<string, AudioData> audio = new Dictionary<string, AudioData>();

        public string emotion = "";
        public string environment = "";
        public string voice_effect = "none";
        public float intensity = 5.0f;
        public string middleware_event = "";
        public List<string> tags = new List<string>();

        public List<Dictionary<string, object>> choices = new List<Dictionary<string, object>>();
        public Dictionary<string, object> condition_logic = new Dictionary<string, object>();

        public string label = "";
        public string description = "";
        public string location = "";
        public string quest = "";
        public string default_emotion = "";
        public string default_environment = "";

        public List<Dictionary<string, object>> next_nodes = new List<Dictionary<string, object>>();

        public class AudioData
        {
            public string file;
            public float duration;
        }

        public static GameVocalDialogueLine FromDictionary(JObject d)
        {
            var line = new GameVocalDialogueLine();
            line.id = d["id"]?.ToString() ?? "";
            line.type = d["type"]?.ToString() ?? "";
            line.text = d["text"]?.ToString() ?? "";
            line.character_id = d["character_id"]?.ToString() ?? "";
            line.line_type = d["line_type"]?.ToString() ?? "voiced";

            if (d["audio"] != null && d["audio"].Type == JTokenType.Object)
            {
                var audioDict = (JObject)d["audio"];
                foreach (var prop in audioDict.Properties())
                {
                    var val = prop.Value as JObject;
                    if (val != null)
                    {
                        line.audio[prop.Name] = new AudioData
                        {
                            file = val["file"]?.ToString() ?? "",
                            duration = val["duration"]?.Value<float>() ?? 0f
                        };
                    }
                }
            }

            line.emotion = d["emotion"]?.ToString() ?? "";
            line.environment = d["environment"]?.ToString() ?? "";
            line.voice_effect = d["voiceEffect"]?.ToString() ?? d["voice_effect"]?.ToString() ?? "none";
            line.intensity = d["intensity"]?.Value<float>() ?? 5.0f;
            line.middleware_event = d["middleware_event"]?.ToString() ?? "";

            if (d["tags"] != null && d["tags"].Type == JTokenType.Array)
            {
                foreach (var tag in d["tags"]) line.tags.Add(tag.ToString());
            }

            line.choices = ParseObjectList(d["choices"] as JArray);
            
            if (d["condition_logic"] != null && d["condition_logic"].Type == JTokenType.Object)
            {
                line.condition_logic = (d["condition_logic"] as JObject).ToObject<Dictionary<string, object>>();
            }

            line.label = d["label"]?.ToString() ?? "";
            line.description = d["description"]?.ToString() ?? "";
            line.location = d["location"]?.ToString() ?? "";
            line.quest = d["quest"]?.ToString() ?? "";
            line.default_emotion = d["default_emotion"]?.ToString() ?? "";
            line.default_environment = d["default_environment"]?.ToString() ?? "";

            line.next_nodes = ParseObjectList(d["next_nodes"] as JArray);

            return line;
        }

        private static List<Dictionary<string, object>> ParseObjectList(JArray arr)
        {
            var list = new List<Dictionary<string, object>>();
            if (arr != null)
            {
                foreach (var item in arr)
                {
                    if (item.Type == JTokenType.Object)
                    {
                        list.Add(item.ToObject<Dictionary<string, object>>());
                    }
                }
            }
            return list;
        }

        public string GetAudioPath(string langCode = "en")
        {
            if (audio.TryGetValue(langCode, out var data)) return data.file;
            if (audio.TryGetValue("en", out data)) return data.file;
            return "";
        }

        public float GetAudioDuration(string langCode = "en")
        {
            if (audio.TryGetValue(langCode, out var data)) return data.duration;
            if (audio.TryGetValue("en", out data)) return data.duration;
            return 0f;
        }

        public bool HasChoices()
        {
            return type == "choiceNode" && choices.Count > 0;
        }

        public bool IsCondition()
        {
            return type == "conditionNode";
        }
    }
}
