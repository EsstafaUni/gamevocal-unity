using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameVocal
{
    public class GameVocalManager : MonoBehaviour
    {
        private static GameVocalManager _instance;
        public static GameVocalManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("GameVocalManager");
                    _instance = go.AddComponent<GameVocalManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // --- Configuration ---
        public string LanguageCode { get; set; } = "en";
        public bool AutoAdvance { get; set; } = false;
        public float FallbackAdvanceTime { get; set; } = 2.0f;

        // --- Events ---
        public event Action<GameVocalDialogueLine, string> OnLineStarted;
        public event Action<GameVocalDialogueLine, string> OnLineFinished;
        public event Action<List<Dictionary<string, object>>, GameVocalDialogueLine, string> OnChoicesPresented;
        public event Action<bool, GameVocalDialogueLine, string> OnConditionEvaluated;
        public event Action<GameVocalDialogueLine, string> OnSceneEntered;
        public event Action<string> OnTreeFinished;
        public event Action<string, object> OnVariableChanged;

        // --- State ---
        private class TreeData
        {
            public string rootNodeId;
            public string name;
            public Dictionary<string, GameVocalDialogueLine> nodes = new Dictionary<string, GameVocalDialogueLine>();
        }

        private Dictionary<string, TreeData> _trees = new Dictionary<string, TreeData>();
        private Dictionary<string, JObject> _characters = new Dictionary<string, JObject>();
        private Dictionary<string, object> _variables = new Dictionary<string, object>();

        private string _currentTreeId = "";
        private string _currentNodeId = "";
        private bool _isPlaying = false;
        private bool _awaitingChoice = false;
        private Coroutine _autoAdvanceCoroutine;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // --- Public API ---

        public bool LoadDialogue(string relativePath = "dialogue.json")
        {
            string path = GameVocalPathUtils.GetAbsolutePath(relativePath);
            if (!File.Exists(path))
            {
                Debug.LogError($"[GameVocal] Cannot find dialogue file: {path}");
                return false;
            }

            try
            {
                string jsonString = File.ReadAllText(path);
                JObject data = JObject.Parse(jsonString);

                _trees.Clear();
                _characters.Clear();

                if (data["characters"] is JArray charsArray)
                {
                    foreach (JObject charDict in charsArray)
                    {
                        string cid = charDict["id"]?.ToString();
                        if (!string.IsNullOrEmpty(cid))
                            _characters[cid] = charDict;
                    }
                }

                JArray dialogues = data["dialogues"] as JArray;
                if ((dialogues == null || dialogues.Count == 0) && data["root_node_id"] != null)
                {
                    dialogues = new JArray { data };
                }

                if (dialogues != null)
                {
                    foreach (JObject treeDict in dialogues)
                    {
                        string tid = treeDict["dialogue_id"]?.ToString() ?? treeDict["id"]?.ToString();
                        if (string.IsNullOrEmpty(tid)) continue;

                        var treeData = new TreeData
                        {
                            rootNodeId = treeDict["root_node_id"]?.ToString() ?? "",
                            name = treeDict["name"]?.ToString() ?? ""
                        };

                        if (treeDict["nodes"] is JArray nodesArray)
                        {
                            foreach (JObject nodeDict in nodesArray)
                            {
                                string nid = nodeDict["id"]?.ToString();
                                if (!string.IsNullOrEmpty(nid))
                                {
                                    treeData.nodes[nid] = GameVocalDialogueLine.FromDictionary(nodeDict);
                                }
                            }
                        }
                        _trees[tid] = treeData;
                    }
                }

                Debug.Log($"[GameVocal] Loaded {_trees.Count} trees, {_characters.Count} characters from {path}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameVocal] Failed to parse dialogue JSON: {ex.Message}");
                return false;
            }
        }

        public bool IsLoaded() => _trees.Count > 0;
        public List<string> ListTrees() => _trees.Keys.ToList();
        
        public void PlayTree(string treeId)
        {
            if (!_trees.ContainsKey(treeId))
            {
                Debug.LogError($"[GameVocal] Tree not found: {treeId}");
                return;
            }

            Stop();
            _currentTreeId = treeId;
            _isPlaying = true;
            _awaitingChoice = false;

            var tree = _trees[treeId];
            string rootId = tree.rootNodeId;
            if (string.IsNullOrEmpty(rootId) && tree.nodes.Count > 0)
            {
                rootId = tree.nodes.Keys.First();
            }

            Visit(treeId, rootId);
        }

        public void Stop()
        {
            if (_autoAdvanceCoroutine != null)
            {
                StopCoroutine(_autoAdvanceCoroutine);
                _autoAdvanceCoroutine = null;
            }
            _isPlaying = false;
            _awaitingChoice = false;
            _currentTreeId = "";
            _currentNodeId = "";
        }

        public void Advance()
        {
            if (!_isPlaying || _awaitingChoice) return;
            if (_autoAdvanceCoroutine != null)
            {
                StopCoroutine(_autoAdvanceCoroutine);
                _autoAdvanceCoroutine = null;
            }
            OnAutoAdvanceTimeout(_currentTreeId, _currentNodeId);
        }

        public void SelectChoice(int visibleIndex)
        {
            if (!_awaitingChoice || string.IsNullOrEmpty(_currentTreeId))
            {
                Debug.LogWarning("[GameVocal] SelectChoice() called when not awaiting a choice.");
                return;
            }

            var node = GetNode(_currentTreeId, _currentNodeId);
            if (node == null) return;

            var visibleChoices = GetVisibleChoices(node);
            if (visibleIndex < 0 || visibleIndex >= visibleChoices.Count)
            {
                Debug.LogError($"[GameVocal] Choice index {visibleIndex} out of range ({visibleChoices.Count} visible).");
                return;
            }

            var chosen = visibleChoices[visibleIndex];
            int choiceIdx = chosen.ContainsKey("_choice_index") ? Convert.ToInt32(chosen["_choice_index"]) : visibleIndex;

            if (chosen.TryGetValue("mutations", out object mutObj) && mutObj is List<Dictionary<string, object>> mutations)
            {
                ApplyMutations(mutations);
            }

            string targetId = "";
            foreach (var nn in node.next_nodes)
            {
                if (nn.TryGetValue("choice_index", out object cIdxObj) && Convert.ToInt32(cIdxObj) == choiceIdx)
                {
                    targetId = nn.TryGetValue("id", out object idObj) ? idObj.ToString() : "";
                    break;
                }
            }

            if (string.IsNullOrEmpty(targetId) && choiceIdx < node.next_nodes.Count)
            {
                targetId = node.next_nodes[choiceIdx].TryGetValue("id", out object idObj) ? idObj.ToString() : "";
            }

            _awaitingChoice = false;

            if (string.IsNullOrEmpty(targetId))
                FinishTree(_currentTreeId);
            else
                Visit(_currentTreeId, targetId);
        }

        // --- Variable API ---

        public void SetVariable(string name, object value)
        {
            _variables.TryGetValue(name, out object old);
            _variables[name] = value;
            if (!Equals(old, value))
            {
                OnVariableChanged?.Invoke(name, value);
            }
        }

        public object GetVariable(string name, object defaultValue = null)
        {
            return _variables.TryGetValue(name, out object val) ? val : defaultValue;
        }

        public bool HasVariable(string name) => _variables.ContainsKey(name);
        public Dictionary<string, object> GetAllVariables() => new Dictionary<string, object>(_variables);
        public void LoadVariables(Dictionary<string, object> vars) => _variables = new Dictionary<string, object>(vars);

        // --- Internal Graph Walker ---

        private GameVocalDialogueLine GetNode(string treeId, string nodeId)
        {
            if (_trees.TryGetValue(treeId, out var tree) && tree.nodes.TryGetValue(nodeId, out var node))
                return node;
            return null;
        }

        private void Visit(string treeId, string nodeId)
        {
            var node = GetNode(treeId, nodeId);
            if (node == null)
            {
                Debug.LogWarning($"[GameVocal] Node not found: {nodeId} in tree: {treeId}");
                FinishTree(treeId);
                return;
            }

            _currentNodeId = nodeId;

            switch (node.type)
            {
                case "startNode":
                    AdvanceToNext(treeId, node);
                    break;
                case "endNode":
                    FinishTree(treeId);
                    break;
                case "npcNode":
                    HandleNpcNode(treeId, node);
                    break;
                case "choiceNode":
                    HandleChoiceNode(treeId, node);
                    break;
                case "conditionNode":
                    HandleConditionNode(treeId, node);
                    break;
                case "parentContainerNode":
                    OnSceneEntered?.Invoke(node, treeId);
                    AdvanceToNext(treeId, node);
                    break;
                default:
                    OnLineStarted?.Invoke(node, treeId);
                    ScheduleAdvance(treeId, node, 0f);
                    break;
            }
        }

        private void HandleNpcNode(string treeId, GameVocalDialogueLine node)
        {
            OnLineStarted?.Invoke(node, treeId);

            if (AutoAdvance)
            {
                float duration = node.GetAudioDuration(LanguageCode);
                ScheduleAdvance(treeId, node, duration);
            }
        }

        private void HandleChoiceNode(string treeId, GameVocalDialogueLine node)
        {
            var visible = GetVisibleChoices(node);
            if (visible.Count == 0)
            {
                Debug.LogWarning($"[GameVocal] choiceNode {node.id}: no visible choices. Auto-advancing.");
                AdvanceToNext(treeId, node);
                return;
            }

            _awaitingChoice = true;
            OnChoicesPresented?.Invoke(visible, node, treeId);
        }

        private void HandleConditionNode(string treeId, GameVocalDialogueLine node)
        {
            bool result = EvaluateConditionLogic(node.condition_logic);
            OnConditionEvaluated?.Invoke(result, node, treeId);

            string branchLabel = result ? "true" : "false";
            string targetId = "";

            foreach (var nn in node.next_nodes)
            {
                if (nn.TryGetValue("branch", out object br) && br.ToString() == branchLabel)
                {
                    targetId = nn.TryGetValue("id", out object id) ? id.ToString() : "";
                    break;
                }
            }

            if (string.IsNullOrEmpty(targetId) && node.next_nodes.Count > 0)
            {
                targetId = node.next_nodes[0].TryGetValue("id", out object id) ? id.ToString() : "";
            }

            if (string.IsNullOrEmpty(targetId))
                FinishTree(treeId);
            else
                Visit(treeId, targetId);
        }

        private void AdvanceToNext(string treeId, GameVocalDialogueLine node)
        {
            if (node.next_nodes.Count == 0)
            {
                FinishTree(treeId);
                return;
            }
            string nextId = node.next_nodes[0].TryGetValue("id", out object id) ? id.ToString() : "";
            if (string.IsNullOrEmpty(nextId))
                FinishTree(treeId);
            else
                Visit(treeId, nextId);
        }

        private void ScheduleAdvance(string treeId, GameVocalDialogueLine node, float duration)
        {
            float wait = duration > 0f ? duration : FallbackAdvanceTime;
            if (_autoAdvanceCoroutine != null) StopCoroutine(_autoAdvanceCoroutine);
            _autoAdvanceCoroutine = StartCoroutine(AutoAdvanceRoutine(treeId, node.id, wait));
        }

        private IEnumerator AutoAdvanceRoutine(string treeId, string nodeId, float wait)
        {
            yield return new WaitForSeconds(wait);
            OnAutoAdvanceTimeout(treeId, nodeId);
        }

        private void OnAutoAdvanceTimeout(string treeId, string nodeId)
        {
            if (!_isPlaying || _awaitingChoice) return;
            // Prevent timeout from firing for a previous node if state changed
            if (treeId != _currentTreeId || nodeId != _currentNodeId) return;

            var node = GetNode(treeId, nodeId);
            if (node == null)
            {
                FinishTree(treeId);
                return;
            }
            
            OnLineFinished?.Invoke(node, treeId);
            AdvanceToNext(treeId, node);
        }

        private void FinishTree(string treeId)
        {
            if (_autoAdvanceCoroutine != null)
            {
                StopCoroutine(_autoAdvanceCoroutine);
                _autoAdvanceCoroutine = null;
            }
            _isPlaying = false;
            _awaitingChoice = false;
            OnTreeFinished?.Invoke(treeId);
        }

        // --- Condition & Mutation Logic ---

        private bool EvaluateConditionLogic(Dictionary<string, object> logic)
        {
            if (logic == null || logic.Count == 0) return true;

            string combinator = logic.TryGetValue("combinator", out object c) ? c.ToString() : "AND";
            var conditions = logic.TryGetValue("conditions", out object conds) ? conds as JArray : null;

            if (conditions == null || conditions.Count == 0) return true;

            var results = new List<bool>();
            foreach (JObject cond in conditions)
            {
                results.Add(EvaluateSingleCondition(cond.ToObject<Dictionary<string, object>>()));
            }

            if (combinator == "OR") return results.Any(r => r);
            return results.All(r => r);
        }

        private bool EvaluateSingleCondition(Dictionary<string, object> cond)
        {
            string variable = cond.TryGetValue("variable", out object v) ? v.ToString() : "";
            string op = cond.TryGetValue("operator", out object o) ? o.ToString() : "==";
            string rhsRaw = cond.TryGetValue("value", out object val) ? val.ToString() : "";

            if (string.IsNullOrEmpty(variable)) return true;

            object lhs = GetVariable(variable, "");
            object rhs = CoerceValue(rhsRaw, lhs);

            switch (op)
            {
                case "==": return Equals(lhs, rhs);
                case "!=": return !Equals(lhs, rhs);
                case ">": return ToFloat(lhs) > ToFloat(rhs);
                case "<": return ToFloat(lhs) < ToFloat(rhs);
                case ">=": return ToFloat(lhs) >= ToFloat(rhs);
                case "<=": return ToFloat(lhs) <= ToFloat(rhs);
                default: return false;
            }
        }

        private void ApplyMutations(List<Dictionary<string, object>> mutations)
        {
            foreach (var m in mutations)
            {
                string variable = m.TryGetValue("variable", out object v) ? v.ToString() : "";
                if (string.IsNullOrEmpty(variable)) continue;

                string op = m.TryGetValue("operation", out object o) ? o.ToString() : "set";
                string valRaw = m.TryGetValue("value", out object val) ? val.ToString() : "";

                object current = GetVariable(variable, 0f);
                object newVal = CoerceValue(valRaw, current);

                switch (op)
                {
                    case "set": SetVariable(variable, newVal); break;
                    case "add": SetVariable(variable, ToFloat(current) + ToFloat(newVal)); break;
                    case "subtract": SetVariable(variable, ToFloat(current) - ToFloat(newVal)); break;
                    case "multiply": SetVariable(variable, ToFloat(current) * ToFloat(newVal)); break;
                    case "toggle": SetVariable(variable, !ToBool(current)); break;
                    default: SetVariable(variable, newVal); break;
                }
            }
        }

        private List<Dictionary<string, object>> GetVisibleChoices(GameVocalDialogueLine node)
        {
            var outList = new List<Dictionary<string, object>>();
            for (int i = 0; i < node.choices.Count; i++)
            {
                var choice = node.choices[i];
                string visibility = choice.TryGetValue("visibility", out object v) ? v.ToString() : "always";

                if (visibility == "hidden") continue;

                if (visibility == "conditional" || (choice.ContainsKey("condition") && choice["condition"] is Dictionary<string, object>))
                {
                    if (choice.TryGetValue("condition", out object condObj) && condObj is Dictionary<string, object> cond)
                    {
                        if (cond.Count > 0)
                        {
                            string mode = cond.TryGetValue("mode", out object modeObj) ? modeObj.ToString() : "visual";
                            if (mode == "visual" && !EvaluateSingleCondition(cond)) continue;
                        }
                    }
                }

                var entry = new Dictionary<string, object>(choice);
                entry["_choice_index"] = i;
                outList.Add(entry);
            }
            return outList;
        }

        private object CoerceValue(string raw, object reference)
        {
            if (raw == "true") return true;
            if (raw == "false") return false;
            if (float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fVal))
            {
                if (reference is int && !raw.Contains("."))
                    return Mathf.RoundToInt(fVal);
                return fVal;
            }
            return raw;
        }

        private float ToFloat(object v)
        {
            if (v is bool b) return b ? 1f : 0f;
            if (v is IConvertible c) return c.ToSingle(null);
            return 0f;
        }

        private bool ToBool(object v)
        {
            if (v is bool b) return b;
            if (v is string s) return s == "true";
            return ToFloat(v) > 0.5f;
        }
    }
}
