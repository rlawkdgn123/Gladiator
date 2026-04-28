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
