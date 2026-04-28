using UnityEngine;
using UnityEditor;

public class TerrainAutoPaint : EditorWindow
{
    Terrain terrain;

    // 레이어 인덱스 (Paint Texture에 추가된 순서대로)
    int gravelIdx = 0;    // 베이스
    int soilIdx = 1;      // 중간 빈도
    int sandStoneIdx = 2; // 가끔

    // 비율
    float gravelBase = 0.75f;     // Gravel 기본 75%
    float soilIntensity = 0.35f;  // Soil 최대 35%
    float sandStoneIntensity = 0.15f; // SandStone 최대 15%

    // 노이즈 스케일 (값 작을수록 큰 덩어리)
    float soilNoiseScale = 0.015f;
    float sandStoneNoiseScale = 0.04f;

    // 시드
    int seed = 12345;

    [MenuItem("Tools/Terrain/Auto Paint (Gravel base + scatter)")]
    static void Open() => GetWindow<TerrainAutoPaint>("Terrain Auto Paint");

    void OnGUI()
    {
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Layer Indices (Paint Texture 순서)", EditorStyles.boldLabel);
        gravelIdx = EditorGUILayout.IntField("Gravel Index (base)", gravelIdx);
        soilIdx = EditorGUILayout.IntField("Soil Index", soilIdx);
        sandStoneIdx = EditorGUILayout.IntField("SandStone Index", sandStoneIdx);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Intensity", EditorStyles.boldLabel);
        gravelBase = EditorGUILayout.Slider("Gravel Base", gravelBase, 0.3f, 1f);
        soilIntensity = EditorGUILayout.Slider("Soil Max", soilIntensity, 0f, 0.8f);
        sandStoneIntensity = EditorGUILayout.Slider("SandStone Max", sandStoneIntensity, 0f, 0.5f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Noise", EditorStyles.boldLabel);
        soilNoiseScale = EditorGUILayout.Slider("Soil Noise Scale", soilNoiseScale, 0.005f, 0.1f);
        sandStoneNoiseScale = EditorGUILayout.Slider("SandStone Noise Scale", sandStoneNoiseScale, 0.01f, 0.2f);
        seed = EditorGUILayout.IntField("Seed", seed);

        EditorGUILayout.Space();
        if (GUILayout.Button("Paint!", GUILayout.Height(30)))
            Paint();
    }

    void Paint()
    {
        if (terrain == null) { Debug.LogError("Terrain 지정 필요"); return; }
        var data = terrain.terrainData;
        int w = data.alphamapWidth;
        int h = data.alphamapHeight;
        int layers = data.alphamapLayers;

        Debug.Log($"[AutoPaint] alphamap={w}x{h}, layers={layers}, indices: gravel={gravelIdx}, soil={soilIdx}, sand={sandStoneIdx}");

        if (layers < 3)
        {
            Debug.LogError($"레이어가 {layers}개뿐임. 최소 3개 필요");
            return;
        }

        System.Random rng = new System.Random(seed);
        float offA = (float)rng.NextDouble() * 1000f;
        float offB = (float)rng.NextDouble() * 1000f;
        float offC = (float)rng.NextDouble() * 1000f;
        float offD = (float)rng.NextDouble() * 1000f;

        float[,,] alpha = new float[h, w, layers];

        // 디버그용 중앙값
        float dbgSoil = 0f, dbgSand = 0f;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // Soil: 2옥타브 FBM → 강한 대비
                float s1 = Mathf.PerlinNoise((x + offA) * soilNoiseScale, (y + offB) * soilNoiseScale);
                float s2 = Mathf.PerlinNoise((x + offA) * soilNoiseScale * 2.3f, (y + offB) * soilNoiseScale * 2.3f);
                float nSoil = s1 * 0.65f + s2 * 0.35f;
                nSoil = Mathf.Clamp01((nSoil - 0.35f) * 3.5f);  // 강한 대비
                nSoil *= soilIntensity * 2.5f;

                // SandStone: 더 드물게, 대비 강하게
                float sd1 = Mathf.PerlinNoise((x + offC) * sandStoneNoiseScale, (y + offD) * sandStoneNoiseScale);
                float nSand = Mathf.Clamp01((sd1 - 0.55f) * 4f);  // 상위 45%만 표현
                nSand *= sandStoneIntensity * 3f;

                float[] w_ = new float[layers];
                w_[gravelIdx] = gravelBase;
                if (soilIdx < layers) w_[soilIdx] = nSoil;
                if (sandStoneIdx < layers) w_[sandStoneIdx] = nSand;

                float sum = 0f;
                for (int i = 0; i < layers; i++) sum += w_[i];
                if (sum < 0.0001f) { w_[gravelIdx] = 1f; sum = 1f; }
                for (int i = 0; i < layers; i++) alpha[y, x, i] = w_[i] / sum;

                if (x == w / 2 && y == h / 2) { dbgSoil = nSoil; dbgSand = nSand; }
            }

        Debug.Log($"[AutoPaint] 중앙 픽셀 weights: gravel={gravelBase:F2}, soil={dbgSoil:F2}, sand={dbgSand:F2}");

        Undo.RegisterCompleteObjectUndo(data, "Auto Paint Terrain");
        data.SetAlphamaps(0, 0, alpha);

        // 강제 리프레시 — SetAlphamaps만으론 splatmap 텍스처가 갱신 안 되는 경우가 있음
        terrain.Flush();
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(terrain);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();

        Debug.Log($"[AutoPaint] Painted {w}x{h}, {layers} layers (flushed)");
    }
}