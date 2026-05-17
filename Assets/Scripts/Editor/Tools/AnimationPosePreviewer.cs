/*
 *
 * AI-Generated Code
 *
 */

using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;
using System.Collections.Generic;

namespace Assets.Scripts.Editor.Tools
{
    public class AnimationPosePreviewer : EditorWindow
    {
        private GameObject _targetObject;
        private Animator _animator;
        private AnimationClip _selectedClip;
        private List<AnimationClip> _animationClips = new();
        private Vector2 _scrollPos;
        private bool _includeChildren;

        [MenuItem("Tools/Animation Pose Previewer")]
        private static void Init()
        {
            AnimationPosePreviewer window = GetWindow<AnimationPosePreviewer>("Pose Previewer");
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Pose Preview Tool", EditorStyles.boldLabel);
            _targetObject = (GameObject)EditorGUILayout.ObjectField("Target Rigged Object", _targetObject, typeof(GameObject), true);

            _includeChildren = EditorGUILayout.Toggle("Include Children", _includeChildren);

            if (_targetObject != null)
            {
                _animator = _targetObject.GetComponent<Animator>();
                if (_animator == null)
                {
                    EditorGUILayout.HelpBox("The selected GameObject does not have an Animator component.", MessageType.Warning);
                    return;
                }

                if (GUILayout.Button("Load Animations"))
                {
                    LoadAnimations(_animator);
                }

                if (_animationClips.Count > 0)
                {
                    EditorGUILayout.Space();
                    _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(150));

                    foreach (var clip in _animationClips)
                    {
                        if (GUILayout.Button(clip.name, (_selectedClip == clip) ? EditorStyles.toolbarButton : EditorStyles.miniButton))
                        {
                            _selectedClip = clip;
                        }
                    }

                    EditorGUILayout.EndScrollView();

                    EditorGUILayout.Space();
                    if (_selectedClip != null)
                    {
                        if (GUILayout.Button("Apply First Frame Pose"))
                        {
                            ApplyPose(_selectedClip);
                        }
                    }
                }
            }
        }

        private void LoadAnimations(Animator animator)
        {
            _animationClips.Clear();
            _selectedClip = null;

            var controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null)
            {
                Debug.LogWarning("Animator Controller is not an AnimatorController (might be an AnimatorOverrideController).");
                return;
            }

            // Get all animation clips from all layers and states
            foreach (var layer in controller.layers)
            {
                foreach (var state in layer.stateMachine.states)
                {
                    var clip = state.state.motion as AnimationClip;
                    if (clip != null && !_animationClips.Contains(clip))
                    {
                        _animationClips.Add(clip);
                    }
                }
            }

            if (_animationClips.Count == 0)
            {
                Debug.LogWarning("No animation clips found in Animator Controller.");
            }
        }

        private void ApplyPose(AnimationClip clip)
        {
            if (_targetObject == null || clip == null)
            {
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(_targetObject, "Apply First Frame Pose");

            Dictionary<string, Transform> targetTransforms = new Dictionary<string, Transform>();
            CollectTransforms(_targetObject.transform, "", targetTransforms);

            var bindings = AnimationUtility.GetCurveBindings(clip);

            foreach (var binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                float value = curve.Evaluate(0f); // First frame

                if (!targetTransforms.TryGetValue(binding.path, out Transform targetTransform))
                    continue;

                if (binding.type == typeof(Transform))
                {
                    if (binding.propertyName.StartsWith("m_LocalPosition"))
                    {
                        Vector3 pos = targetTransform.localPosition;
                        if (binding.propertyName.EndsWith(".x")) pos.x = value;
                        else if (binding.propertyName.EndsWith(".y")) pos.y = value;
                        else if (binding.propertyName.EndsWith(".z")) pos.z = value;
                        targetTransform.localPosition = pos;
                    }
                    else if (binding.propertyName.StartsWith("m_LocalScale"))
                    {
                        Vector3 scale = targetTransform.localScale;
                        if (binding.propertyName.EndsWith(".x")) scale.x = value;
                        else if (binding.propertyName.EndsWith(".y")) scale.y = value;
                        else if (binding.propertyName.EndsWith(".z")) scale.z = value;
                        targetTransform.localScale = scale;
                    }
                    else if (binding.propertyName.StartsWith("m_LocalRotation"))
                    {
                        Quaternion rot = targetTransform.localRotation;
                        Vector4 q = new Vector4(rot.x, rot.y, rot.z, rot.w);
                        if (binding.propertyName.EndsWith(".x")) q.x = value;
                        else if (binding.propertyName.EndsWith(".y")) q.y = value;
                        else if (binding.propertyName.EndsWith(".z")) q.z = value;
                        else if (binding.propertyName.EndsWith(".w")) q.w = value;
                        targetTransform.localRotation = new Quaternion(q.x, q.y, q.z, q.w);
                    }
                }
                else
                {
                    Component component = targetTransform.GetComponent(binding.type);
                    if (component != null)
                    {
                        SerializedObject so = new SerializedObject(component);
                        SerializedProperty prop = so.FindProperty(binding.propertyName);
                        if (prop != null && prop.propertyType == SerializedPropertyType.Float)
                        {
                            prop.floatValue = value;
                            so.ApplyModifiedProperties();
                        }
                    }
                }
            }

            EditorUtility.SetDirty(_targetObject);
        }

        private void CollectTransforms(Transform root, string path, Dictionary<string, Transform> dict)
        {
            dict[path] = root;

            if (!_includeChildren)
            {
                return;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                string childPath = string.IsNullOrEmpty(path) ? child.name : $"{path}/{child.name}";
                CollectTransforms(child, childPath, dict);
            }
        }
    }
}
