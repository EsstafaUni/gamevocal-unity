using UnityEditor;
using UnityEngine;

namespace GameVocal.Editor
{
    public static class GameVocalEditorStyles
    {
        public static GUIStyle HeaderLabel
        {
            get
            {
                var style = new GUIStyle(EditorStyles.boldLabel);
                style.fontSize = 14;
                style.margin = new RectOffset(0, 0, 5, 5);
                return style;
            }
        }

        public static Color ThemeGreen = new Color(0.56f, 0.82f, 0.18f); // #90d12e
    }
}
