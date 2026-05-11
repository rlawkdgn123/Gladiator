using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// DisableField attribute가 붙은 필드를 Inspector에서 비활성화된 상태로 그려준다.
// attribute 타입은 런타임 스크립트에서도 참조해야 하므로 Editor 폴더 밖에 두고,
// 여기서는 에디터 전용 drawer 역할만 담당한다.
[CustomPropertyDrawer(typeof(DisableField))]
public class CustomInspectorModule : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        DisableField disableFieldAttribute = (DisableField)attribute;

        bool shouldDisable =
            (Application.isPlaying && disableFieldAttribute.disableInPlayMode) ||
            (!Application.isPlaying && disableFieldAttribute.disableInEditMode);

        // Property wrapper를 씌워두면 prefab override 같은 기본 Inspector 동작을 유지할 수 있다.
        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginDisabledGroup(shouldDisable);
        EditorGUI.PropertyField(position, property, label, true);
        EditorGUI.EndDisabledGroup();
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}

[CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
[CanEditMultipleObjects]
public class DisableFieldInspectorEditor : Editor
{
    static readonly Dictionary<string, bool> Foldouts = new();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        DrawNonSerializedDisableFields();
    }

    void DrawNonSerializedDisableFields()
    {
        bool drewAny = false;

        foreach (FieldInfo field in GetDisableFields(target.GetType()))
        {
            if (serializedObject.FindProperty(field.Name) != null)
                continue;

            object value = field.GetValue(target);
            if (value == null)
                continue;

            if (value is string || value is not IEnumerable enumerable)
                continue;

            if (!drewAny)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Runtime Disable Fields", EditorStyles.boldLabel);
                drewAny = true;
            }

            DrawEnumerableField(field, enumerable);
        }
    }

    static IEnumerable<FieldInfo> GetDisableFields(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (Type current = type; current != null && current != typeof(MonoBehaviour) && current != typeof(Behaviour); current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(flags))
            {
                if (field.IsStatic)
                    continue;

                if (field.GetCustomAttribute<DisableField>() == null)
                    continue;

                yield return field;
            }
        }
    }

    void DrawEnumerableField(FieldInfo field, IEnumerable enumerable)
    {
        List<object> items = new();
        foreach (object item in enumerable)
            items.Add(item);

        string key = $"{target.GetInstanceID()}:{field.DeclaringType?.FullName}.{field.Name}";
        bool expanded = Foldouts.TryGetValue(key, out bool current) && current;

        EditorGUI.BeginDisabledGroup(true);
        expanded = EditorGUILayout.Foldout(expanded, $"{ObjectNames.NicifyVariableName(field.Name)} [{items.Count}]", true);
        Foldouts[key] = expanded;

        if (expanded)
        {
            EditorGUI.indentLevel++;

            if (items.Count == 0)
            {
                EditorGUILayout.LabelField("Empty");
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    object item = items[i];
                    string label = $"Element {i}";

                    if (item is UnityEngine.Object unityObject)
                    {
                        EditorGUILayout.ObjectField(label, unityObject, typeof(UnityEngine.Object), true);
                    }
                    else
                    {
                        EditorGUILayout.TextField(label, item?.ToString() ?? "null");
                    }
                }
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndDisabledGroup();
    }
}
