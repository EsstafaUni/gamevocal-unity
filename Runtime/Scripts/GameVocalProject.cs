using System.Collections.Generic;
using UnityEngine;

namespace GameVocal
{
    [CreateAssetMenu(fileName = "NewProject", menuName = "GameVocal/Project")]
    public class GameVocalProject : ScriptableObject
    {
        [SerializeField] private string projectId;
        [SerializeField] private string schema = "GVDF";
        [SerializeField] private string schemaVersion = "1.0";
        [SerializeField] private string dialogueMode = "voiced";
        [SerializeField] private string projectName;
        [SerializeField] private List<GameVocalCharacter> characters;
        [SerializeField] private List<GameVocalDialogueLine> dialogueLines;

        public string ProjectId => projectId;
        public string Schema => schema;
        public string SchemaVersion => schemaVersion;
        public string DialogueMode => dialogueMode;
        public string ProjectName => projectName;
        public List<GameVocalCharacter> Characters => characters;
        public List<GameVocalDialogueLine> DialogueLines => dialogueLines;

        public void Initialize(string id, string name)
        {
            projectId = id;
            projectName = name;
            characters = new List<GameVocalCharacter>();
            dialogueLines = new List<GameVocalDialogueLine>();
        }

        public void AddCharacter(GameVocalCharacter character)
        {
            if (!characters.Contains(character))
                characters.Add(character);
        }

        public void AddDialogueLine(GameVocalDialogueLine line)
        {
            if (!dialogueLines.Contains(line))
                dialogueLines.Add(line);
        }
    }
}
