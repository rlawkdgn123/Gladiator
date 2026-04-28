using UnityEngine;
using UnityEditor;
using System.IO;

public class MaskMapPacker : EditorWindow
{
    Texture2D arm, disp;
    string outputName = "MaskMap";

    [MenuItem("Tools/Terrain/MaskMap Packer")]
    static void Open() => GetWindow<MaskMapPacker>("MaskMap Packer");

    void OnGUI()
    {
        arm = (Texture2D)EditorGUILayout.ObjectField("ARM (R=AO G=Rough B=Metal)", arm, typeof(Texture2D), false);
        disp = (Texture2D)EditorGUILayout.ObjectField("Displacement / Height", disp, typeof(Texture2D), false);
        outputName = EditorGUILayout.TextField("Output Name", outputName);

        if (GUILayout.Button("Pack MaskMap"))
            Pack();
    }

    void Pack()
    {
        if (arm == null || disp == null)
        {
            Debug.LogError("ARM과 Disp 둘 다 지정 필요");
            return;
        }

        EnableRW(arm);
        EnableRW(disp);

        int w = arm.width, h = arm.height;
        var armPx = arm.GetPixels();
        var dispPx = disp.GetPixels();
        var outPx = new Color[w * h];

        for (int i = 0; i < outPx.Length; i++)
        {
            outPx[i] = new Color(
                armPx[i].b,          // R = Metallic
                armPx[i].r,          // G = AO
                dispPx[i].r,         // B = Height
                1f - armPx[i].g      // A = Smoothness
            );
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        tex.SetPixels(outPx);
        tex.Apply();

        string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(arm));
        string path = $"{dir}/{outputName}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());

        ImportAndConfigureMask(path);
        Debug.Log($"Packed: {path}");
    }

    static void EnableRW(Texture2D t)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t));
        if (!imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }
    }

    static void ImportAndConfigureMask(string path)
    {
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.sRGBTexture = false;
        imp.alphaIsTransparency = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
    }
}