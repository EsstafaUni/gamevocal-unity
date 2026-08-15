using UnityEngine;

namespace GameVocal
{
    [CreateAssetMenu(fileName = "NewCharacterResource", menuName = "GameVocal/Character Resource")]
    public class GameVocalCharacterResource : ScriptableObject
    {
        public string characterId;
        public string displayName;
        [TextArea(3, 10)]
        public string description;
        public string voiceArchetype;
        [TextArea(2, 5)]
        public string personalityTraits;
        [TextArea(3, 10)]
        public string background;
        public string maturityRating;
        public string tags;
        public string referenceAudioUrl;
        [TextArea(3, 10)]
        public string characterNotes;
    }
}
