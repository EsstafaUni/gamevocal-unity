using UnityEditor;
using UnityEngine;

namespace GameVocal.Editor
{
    [CustomEditor(typeof(GameVocalCharacter))]
    public class GameVocalCharacterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GameVocalCharacter character = (GameVocalCharacter)target;

            GUILayout.Space(20);
            GUILayout.Label("Editor Tools", GameVocalEditorStyles.HeaderLabel);

            if (GUILayout.Button("Open ARKit52 Blendshape Mapper", GUILayout.Height(30)))
            {
                GameVocalBlendshapeMapperWindow.ShowWindow(character);
            }

            GUILayout.Space(10);
            GUILayout.Label("Diagnostics", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate Mappings"))
            {
                character.ValidateMappingsNow();
            }
            
            if (GUILayout.Button("Diagnostic Test (100%)"))
            {
                character.RunDiagnosticTest();
            }

            if (GUILayout.Button("Reset Blendshapes"))
            {
                character.ResetBlendshapes();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
