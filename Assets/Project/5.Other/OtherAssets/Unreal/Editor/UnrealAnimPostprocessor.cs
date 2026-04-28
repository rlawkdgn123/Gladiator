using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class UnrealAnimPostprocessor
{
    // 기본 메뉴:
    // 선택한 FBX에 Unreal -> Unity import 기본 설정만 일회성으로 적용합니다.
    private const string MenuPath = "Tools/Unreal/Apply Import Settings To Selected Models";

    // 보조 메뉴:
    // 전방이 반대로 들어온 애니메이션을 위해 clip 방향을 Y축 기준 180도로 추가 보정합니다.
    private const string MenuPathFlip180 = "Tools/Unreal/Apply Import Settings To Selected Models (Forward +180)";

    [MenuItem(MenuPath)]
    private static void ApplyToSelectedModels()
    {
        ApplyToSelectedModels(0f);
    }

    [MenuItem(MenuPathFlip180)]
    private static void ApplyToSelectedModelsFlip180()
    {
        ApplyToSelectedModels(180f);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateApplyToSelectedModels()
    {
        return HasSelectedModelImporter();
    }

    [MenuItem(MenuPathFlip180, true)]
    private static bool ValidateApplyToSelectedModelsFlip180()
    {
        return HasSelectedModelImporter();
    }

    // 현재 선택한 항목 중 FBX(ModelImporter)만 모아서 일괄 적용합니다.
    private static void ApplyToSelectedModels(float orientationOffsetY)
    {
        string[] selectedGuids = Selection.assetGUIDs;
        if (selectedGuids == null || selectedGuids.Length == 0)
        {
            Debug.LogWarning("[Unreal Converter] 선택된 에셋이 없습니다.");
            return;
        }

        List<string> modelPaths = new List<string>();
        foreach (string guid in selectedGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(assetPath))
                continue;

            if (AssetImporter.GetAtPath(assetPath) is ModelImporter)
                modelPaths.Add(assetPath);
        }

        if (modelPaths.Count == 0)
        {
            Debug.LogWarning("[Unreal Converter] 선택한 항목 중 ModelImporter 대상이 없습니다.");
            return;
        }

        int updatedCount = 0;
        foreach (string assetPath in modelPaths)
        {
            if (ApplyImportSettings(assetPath, orientationOffsetY))
                updatedCount++;
        }

        Debug.Log($"[Unreal Converter] 총 {updatedCount}개 모델에 설정을 적용했습니다. (Orientation Offset Y: {orientationOffsetY})");
    }

    // 메뉴 활성화 조건: 선택한 항목 안에 ModelImporter가 하나라도 있으면 true.
    private static bool HasSelectedModelImporter()
    {
        string[] selectedGuids = Selection.assetGUIDs;
        if (selectedGuids == null || selectedGuids.Length == 0)
            return false;

        foreach (string guid in selectedGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(assetPath) is ModelImporter)
                return true;
        }

        return false;
    }

    // 실제 import 설정 적용 함수.
    // Unreal FBX를 Unity 프로젝트에서 공통 규칙으로 가져오기 위한 기본값을 맞춥니다.
    private static bool ApplyImportSettings(string assetPath, float orientationOffsetY)
    {
        if (AssetImporter.GetAtPath(assetPath) is not ModelImporter importer)
            return false;

        // 좌표축 보정: Unreal의 축계를 Unity 기준으로 bake합니다.
        importer.bakeAxisConversion = true;

        // 현재 프로젝트는 Humanoid 사용 전제라 이 부분도 함께 맞춥니다.
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.animationCompression = ModelImporterAnimationCompression.Off;

        // 공개 API로 접근 가능한 clip 설정들.
        ModelImporterClipAnimation[] clipAnimations = importer.defaultClipAnimations;
        for (int i = 0; i < clipAnimations.Length; i++)
        {
            clipAnimations[i].lockRootHeightY = true;
            clipAnimations[i].lockRootPositionXZ = false;
            clipAnimations[i].lockRootRotation = true;
            clipAnimations[i].name = clipAnimations[i].takeName;
        }

        importer.clipAnimations = clipAnimations;

        // keepOriginalOrientation / orientationOffsetY는 meta에는 저장되지만
        // ModelImporterClipAnimation 공개 프로퍼티로는 직접 접근이 안 됩니다.
        // 그래서 SerializedObject로 내부 직렬화 필드를 수정합니다.
        ApplyClipOrientationSettings(importer, orientationOffsetY);

        importer.SaveAndReimport();

        Debug.Log($"[Unreal Converter] 설정 적용 및 재임포트 완료: {assetPath}");
        return true;
    }

    // 클립의 방향 해석 관련 설정을 직접 맞춰주는 보조 함수.
    // keepOriginalOrientation을 끄고, 필요하면 orientationOffsetY를 180도로 줘서
    // 앞/뒤가 반대로 들어온 애니메이션도 한 번에 보정할 수 있게 합니다.
    private static void ApplyClipOrientationSettings(ModelImporter importer, float orientationOffsetY)
    {
        SerializedObject serializedImporter = new SerializedObject(importer);
        SerializedProperty clipAnimationsProperty = serializedImporter.FindProperty("m_ClipAnimations");
        if (clipAnimationsProperty == null || !clipAnimationsProperty.isArray)
            return;

        for (int i = 0; i < clipAnimationsProperty.arraySize; i++)
        {
            SerializedProperty clipProperty = clipAnimationsProperty.GetArrayElementAtIndex(i);

            SerializedProperty keepOriginalOrientationProperty =
                clipProperty.FindPropertyRelative("keepOriginalOrientation");
            SerializedProperty orientationOffsetProperty =
                clipProperty.FindPropertyRelative("orientationOffsetY");

            if (keepOriginalOrientationProperty != null)
                keepOriginalOrientationProperty.boolValue = false;

            if (orientationOffsetProperty != null)
                orientationOffsetProperty.floatValue = orientationOffsetY;
        }

        serializedImporter.ApplyModifiedPropertiesWithoutUndo();
    }
}
