using Assets.Scripts.Stats;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor.GUI
{
    [CustomPropertyDrawer(typeof(FloatUpgradeableStat))]
    [CustomPropertyDrawer(typeof(IntUpgradeableStat))]
    public class UpgradeableStatDrawer : PropertyDrawer
    {
        private const string RARITY_PROPERTY_NAME = "<Rarity>k__BackingField";
        private const string OVERRIDE_DEFAULT_RARITY_PROPERTY_NAME = "<OverrideDefaultRarity>k__BackingField";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                property.isExpanded,
                label,
                true);

            if (property.isExpanded)
            {
                DrawChildren(position, property);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;

            if (!property.isExpanded)
            {
                return height;
            }

            SerializedProperty currentProperty = property.Copy();
            SerializedProperty endProperty = currentProperty.GetEndProperty();
            bool enterChildren = true;

            while (currentProperty.NextVisible(enterChildren)
                && !SerializedProperty.EqualContents(currentProperty, endProperty))
            {
                enterChildren = false;

                if (!ShouldDrawProperty(property, currentProperty))
                {
                    continue;
                }

                height += EditorGUIUtility.standardVerticalSpacing
                    + EditorGUI.GetPropertyHeight(currentProperty, true);
            }

            return height;
        }

        private static void DrawChildren(Rect position, SerializedProperty property)
        {
            EditorGUI.indentLevel++;

            SerializedProperty currentProperty = property.Copy();
            SerializedProperty endProperty = currentProperty.GetEndProperty();
            bool enterChildren = true;
            float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            while (currentProperty.NextVisible(enterChildren)
                && !SerializedProperty.EqualContents(currentProperty, endProperty))
            {
                enterChildren = false;

                if (!ShouldDrawProperty(property, currentProperty))
                {
                    continue;
                }

                float propertyHeight = EditorGUI.GetPropertyHeight(currentProperty, true);
                Rect propertyPosition = new(position.x, y, position.width, propertyHeight);
                EditorGUI.PropertyField(propertyPosition, currentProperty, true);
                y += propertyHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            EditorGUI.indentLevel--;
        }

        private static bool ShouldDrawProperty(SerializedProperty rootProperty, SerializedProperty currentProperty)
        {
            if (currentProperty.name != RARITY_PROPERTY_NAME)
            {
                return true;
            }

            SerializedProperty overrideDefaultRarity = rootProperty.FindPropertyRelative(OVERRIDE_DEFAULT_RARITY_PROPERTY_NAME);

            return overrideDefaultRarity != null && overrideDefaultRarity.boolValue;
        }
    }
}
