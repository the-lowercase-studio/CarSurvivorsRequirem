/*
 *
 * AI-Generated Code
 *
 */

using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.Tools
{
    public class SortChildrenByName : EditorWindow
    {
        private GameObject _parentObject;

        [MenuItem("Tools/Sort Children By Name")]
        public static void ShowWindow()
        {
            GetWindow<SortChildrenByName>("Sort Children");
        }

        private void OnGUI()
        {
            GUILayout.Label("Sort Children by Name", EditorStyles.boldLabel);
            _parentObject = (GameObject)EditorGUILayout.ObjectField("Parent Object", _parentObject, typeof(GameObject), true);

            if (_parentObject is null)
            {
                EditorGUILayout.HelpBox("Select a parent object to sort its children.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Sort Children"))
            {
                SortChildren(_parentObject);
            }
        }

        private void SortChildren(GameObject parent)
        {
            Undo.RegisterCompleteObjectUndo(parent.transform, "Sort Children");

            List<Transform> children = new();
            foreach (Transform child in parent.transform)
            {
                children.Add(child);
            }

            children.Sort((a, b) =>
            {
                var keyA = GetSortKey(a.name);
                var keyB = GetSortKey(b.name);
                int baseCompare = string.Compare(keyA.baseName, keyB.baseName, System.StringComparison.Ordinal);
                if (baseCompare != 0)
                {
                    return baseCompare;
                }

                return keyA.number.CompareTo(keyB.number);
            });

            for (int i = 0; i < children.Count; i++)
            {
                children[i].SetSiblingIndex(i);
            }
        }

        private (string baseName, int number) GetSortKey(string name)
        {
            var match = Regex.Match(name, @"^(.*?)(?: \((\d+)\))?$");
            string baseName = match.Groups[1].Value.Trim();
            int number = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            return (baseName, number);
        }
    }
}
