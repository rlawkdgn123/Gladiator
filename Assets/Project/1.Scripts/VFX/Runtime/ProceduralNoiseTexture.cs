using UnityEngine;

namespace Game.VFX.Runtime
{
    /// <summary>
    /// 외부 텍스처 에셋 없이 슬래시 노이즈를 프로시저럴로 생성.
    /// Perlin 기반 스트릭 패턴. 한 번 생성하고 캐싱.
    /// </summary>
    public static class ProceduralNoiseTexture
    {
        static Texture2D _slashNoise;

        public static Texture2D GetSlashNoise(int width = 256, int height = 64)
        {
            if (_slashNoise != null) return _slashNoise;

            var tex = new Texture2D(width, height, TextureFormat.R8, mipChain: false, linear: true)
            {
                name = "SlashNoise_Procedural",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[width * height];
            // 가로(U)로 긴 스트릭 느낌: 가로축은 작은 스케일, 세로축은 큰 스케일
            float freqX = 6f;
            float freqY = 2f;
            int octaves = 3;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width;
                    float v = y / (float)height;

                    float amp = 1f;
                    float sum = 0f;
                    float norm = 0f;
                    for (int o = 0; o < octaves; o++)
                    {
                        float scale = Mathf.Pow(2f, o);
                        float n = Mathf.PerlinNoise(u * freqX * scale, v * freqY * scale);
                        sum += n * amp;
                        norm += amp;
                        amp *= 0.55f;
                    }
                    float val = sum / norm;
                    // 대비 강화
                    val = Mathf.SmoothStep(0.25f, 0.85f, val);

                    byte b = (byte)(Mathf.Clamp01(val) * 255f);
                    pixels[y * width + x] = new Color32(b, b, b, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            _slashNoise = tex;
            return _slashNoise;
        }
    }
}
