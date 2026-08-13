/*
 *
 * AI-Generated Code
 *
 */

using UnityEditor;
using UnityEngine;
using System;

namespace Assets.Scripts.Editor.Tools
{
    public class SmartComponentCopier : EditorWindow
    {
        private GameObject _source;
        private GameObject _target;
        private bool _includeChildren;

        [MenuItem("Tools/Smart Copy Components")]
        public static void ShowWindow()
        {
            GetWindow<SmartComponentCopier>("Smart Component Copier");
        }

        private void OnGUI()
        {
            GUILayout.Label("Copy Components with Override Awareness", EditorStyles.boldLabel);

            _source = (GameObject)EditorGUILayout.ObjectField("Source GameObject", _source, typeof(GameObject), true);
            _target = (GameObject)EditorGUILayout.ObjectField("Target GameObject", _target, typeof(GameObject), true);
            _includeChildren = EditorGUILayout.Toggle("Include Children", _includeChildren);

            if (GUILayout.Button("Copy Components"))
            {
                if (_source == null || _target == null)
                {
                    Debug.LogError("Source and Target must be assigned.");
                    return;
                }

                Undo.RegisterFullObjectHierarchyUndo(_target, "Smart Copy Components");

                CopyComponentsRecursive(_source, _target, _includeChildren);
            }
        }

        private void CopyComponentsRecursive(GameObject from, GameObject to, bool recursive)
        {
            CopyComponents(from, to);

            if (!recursive)
            {
                return;
            }

            int childCount = Mathf.Min(from.transform.childCount, to.transform.childCount);

            for (int i = 0; i < childCount; i++)
            {
                GameObject fromChild = from.transform.GetChild(i).gameObject;
                GameObject toChild = to.transform.GetChild(i).gameObject;

                CopyComponentsRecursive(fromChild, toChild, true);
            }
        }

        private void CopyComponents(GameObject from, GameObject to)
        {
            var fromComponents = from.GetComponents<Component>();

            foreach (Component fromComp in fromComponents)
            {
                if (fromComp is Transform)
                {
                    continue;
                }

                Type type = fromComp.GetType();
                Component toComp = to.GetComponent(type);

                if (toComp == null)
                {
                    UnityEditorInternal.ComponentUtility.CopyComponent(fromComp);
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(to);
                }
                else
                {
                    CopySerializedValues(fromComp, toComp);
                }
            }
        }

        private void CopySerializedValues(Component source, Component target)
        {
            SerializedObject srcObject = new SerializedObject(source);
            SerializedObject dstObject = new SerializedObject(target);

            SerializedProperty prop = srcObject.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    if (prop.name == "m_Script")
                    {
                        continue;
                    }

                    SerializedProperty dstProp = dstObject.FindProperty(prop.name);
                    if (dstProp != null)
                    {
                        dstProp.serializedObject.CopyFromSerializedProperty(prop);
                    }
                }
                while (prop.NextVisible(false));
            }

            dstObject.ApplyModifiedProperties();
        }
    }
}
