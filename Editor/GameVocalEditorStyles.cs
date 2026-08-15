using UnityEditor;
using UnityEngine;

namespace GameVocal.Editor
{
    public static class GameVocalEditorStyles
    {
        // Brand Neon Lime color (#C8FF3D)
        public static Color ThemeGreen = new Color(0.784f, 1.0f, 0.239f); 

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

        public static GUIStyle TitleLabel
        {
            get
            {
                var style = new GUIStyle(EditorStyles.boldLabel);
                style.fontSize = 16;
                style.margin = new RectOffset(0, 0, 10, 5);
                return style;
            }
        }

        public static GUIStyle SubtitleLabel
        {
            get
            {
                var style = new GUIStyle(EditorStyles.label);
                style.fontSize = 11;
                style.wordWrap = true;
                style.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                style.margin = new RectOffset(0, 0, 0, 10);
                return style;
            }
        }

        public static GUIStyle SectionBox
        {
            get
            {
                var style = new GUIStyle("helpbox");
                style.padding = new RectOffset(15, 15, 15, 15);
                style.margin = new RectOffset(0, 0, 0, 15);
                return style;
            }
        }

        public static GUIStyle PrimaryButton
        {
            get
            {
                var style = new GUIStyle(GUI.skin.button);
                style.fontSize = 13;
                style.fontStyle = FontStyle.Bold;
                style.padding = new RectOffset(10, 10, 8, 8);
                return style;
            }
        }
        
        public static GUIStyle DangerButton
        {
            get
            {
                var style = new GUIStyle(GUI.skin.button);
                style.fontSize = 12;
                style.normal.textColor = new Color(1.0f, 0.4f, 0.4f);
                style.padding = new RectOffset(10, 10, 5, 5);
                return style;
            }
        }
    }
}
