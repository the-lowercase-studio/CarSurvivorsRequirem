using Assets.Scripts.Navigation.GridSystem;
using UnityEditor;

namespace Assets.Scripts.Editor.GUI
{
    [CustomEditor(typeof(GridManager))]
    public class GridManagerEditor : UnityEditor.Editor
    {
        #region SerializedProperties

        private SerializedProperty _worldGridConfiguration;
        private SerializedProperty _delayBetweenWorldGridUpdate;

        private SerializedProperty _playerGridConfiguration;
        private SerializedProperty _delayBetweenPlayerChunkGridUpdate;

        private SerializedProperty _worldCellBorderColor;
        private SerializedProperty _playerChunkCellBorderColor;
        private SerializedProperty _blockedCellBorderDrawColor;

        private SerializedProperty _flowFieldDebugConfiguration;

        private SerializedProperty _debugGrid;
        private SerializedProperty _debugFlowField;

        #endregion SerializedProperties

        private bool _worldGridGroup;
        private bool _playerGridChunkGroup;
        private bool _debugGroup;

        private const int SPACE_BETWEEN_GROUPS = 2;

        private void OnEnable()
        {
            _worldGridConfiguration = serializedObject.FindProperty(nameof(_worldGridConfiguration));
            _delayBetweenWorldGridUpdate = serializedObject.FindProperty(nameof(_delayBetweenWorldGridUpdate));

            _playerGridConfiguration = serializedObject.FindProperty(nameof(_playerGridConfiguration));
            _delayBetweenPlayerChunkGridUpdate = serializedObject.FindProperty(nameof(_delayBetweenPlayerChunkGridUpdate));

            _worldCellBorderColor = serializedObject.FindProperty(nameof(_worldCellBorderColor));
            _playerChunkCellBorderColor = serializedObject.FindProperty(nameof(_playerChunkCellBorderColor));
            _blockedCellBorderDrawColor = serializedObject.FindProperty(nameof(_blockedCellBorderDrawColor));

            _flowFieldDebugConfiguration = serializedObject.FindProperty(nameof(_flowFieldDebugConfiguration));

            _debugGrid = serializedObject.FindProperty(nameof(_debugGrid));
            _debugFlowField = serializedObject.FindProperty(nameof(_debugFlowField));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            _worldGridGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_worldGridGroup, "World GridSystem Group");
            if (_worldGridGroup)
            {
                EditorGUILayout.PropertyField(_worldGridConfiguration);
                EditorGUILayout.PropertyField(_delayBetweenWorldGridUpdate);
            }
            EditorGUI.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(SPACE_BETWEEN_GROUPS);

            _playerGridChunkGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_playerGridChunkGroup, "Player GridSystem Chunk Group");
            if (_playerGridChunkGroup)
            {
                EditorGUILayout.PropertyField(_playerGridConfiguration);
                EditorGUILayout.PropertyField(_delayBetweenPlayerChunkGridUpdate);
            }
            EditorGUI.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(SPACE_BETWEEN_GROUPS);

            _debugGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_debugGroup, "Debug");
            if (_debugGroup)
            {
                EditorGUILayout.PropertyField(_debugGrid);
                if (_debugGrid.boolValue)
                {
                    EditorGUILayout.LabelField("GridSystem Colors", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_worldCellBorderColor);
                    EditorGUILayout.PropertyField(_playerChunkCellBorderColor);
                    EditorGUILayout.PropertyField(_blockedCellBorderDrawColor);
                }

                EditorGUILayout.PropertyField(_debugFlowField);
                if (_debugFlowField.boolValue)
                {
                    EditorGUILayout.LabelField("FlowField Debug", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_flowFieldDebugConfiguration);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(SPACE_BETWEEN_GROUPS);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
