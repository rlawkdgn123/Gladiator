using UnityEngine;

namespace Game.VFX.Runtime
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ScreenTopLightShaftOverlay : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] Color lightColor = new Color(1f, 0.82f, 0.52f, 1f);
        [Range(0f, 1f)] [SerializeField] float intensity = 0.28f;
        [Range(0.05f, 1f)] [SerializeField] float verticalReach = 0.34f;
        [Range(0.05f, 1f)] [SerializeField] float horizontalSpread = 0.82f;
        [Range(0.1f, 3f)] [SerializeField] float falloff = 1.55f;

        [Header("Position")]
        [Tooltip("Viewport position where the light originates. (0, 0) is bottom-left.")]
        [SerializeField] Vector2 sourceViewportPosition = new Vector2(0.52f, 0.95f);
        [Range(-0.6f, 0.6f)] [SerializeField] float shaftTilt = -0.08f;
        [Range(0.02f, 0.35f)] [SerializeField] float shaftWidth = 0.13f;
        [Range(0f, 1f)] [SerializeField] float topGlow = 0.38f;

        [Header("Quality")]
        [SerializeField] int textureWidth = 512;
        [SerializeField] int textureHeight = 256;

        Texture2D _texture;
        int _lastHash;

        void OnEnable()
        {
            RebuildTexture();
        }

        void OnDisable()
        {
            ReleaseTexture();
        }

        void OnValidate()
        {
            textureWidth = Mathf.Clamp(textureWidth, 64, 2048);
            textureHeight = Mathf.Clamp(textureHeight, 64, 1024);
            RebuildTexture();
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || intensity <= 0f)
                return;

            int hash = ComputeHash();
            if (_texture == null || hash != _lastHash)
                RebuildTexture();

            if (_texture == null)
                return;

            var previousColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _texture, ScaleMode.StretchToFill, true);
            GUI.color = previousColor;
        }

        public void ApplySettings(Color color, float strength, float reach, float spread, float power,
                                  Vector2 sourcePosition, float tilt, float width, float glow)
        {
            lightColor = color;
            intensity = Mathf.Clamp01(strength);
            verticalReach = Mathf.Clamp(reach, 0.05f, 1f);
            horizontalSpread = Mathf.Clamp(spread, 0.05f, 1f);
            falloff = Mathf.Clamp(power, 0.1f, 3f);
            sourceViewportPosition = new Vector2(Mathf.Clamp01(sourcePosition.x), Mathf.Clamp01(sourcePosition.y));
            shaftTilt = Mathf.Clamp(tilt, -0.6f, 0.6f);
            shaftWidth = Mathf.Clamp(width, 0.02f, 0.35f);
            topGlow = Mathf.Clamp01(glow);
            RebuildTexture();
        }

        void RebuildTexture()
        {
            ReleaseTexture();

            _lastHash = ComputeHash();
            _texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false, true)
            {
                name = "ScreenTopLightShaftOverlay",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color baseColor = lightColor;
            var pixels = new Color[textureWidth * textureHeight];

            for (int y = 0; y < textureHeight; y++)
            {
                float v = y / (float)(textureHeight - 1);
                for (int x = 0; x < textureWidth; x++)
                {
                    float u = x / (float)(textureWidth - 1);
                    float alpha = EvaluateAlpha(u, v);
                    pixels[y * textureWidth + x] = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                }
            }

            _texture.SetPixels(pixels);
            _texture.Apply(false, true);
        }

        float EvaluateAlpha(float u, float v)
        {
            float dy = Mathf.Max(0f, sourceViewportPosition.y - v);
            float vertical = Mathf.Clamp01(1f - dy / verticalReach);
            float width = Mathf.Lerp(shaftWidth, horizontalSpread, 1f - vertical);
            float center = sourceViewportPosition.x + shaftTilt * dy;
            float xDistance = Mathf.Abs(u - center);

            float broadGlow = Mathf.SmoothStep(1f, 0f, xDistance / Mathf.Max(0.001f, width));
            float mainShaft = Mathf.SmoothStep(1f, 0f, xDistance / Mathf.Max(0.001f, shaftWidth + dy * 0.35f));
            float topBand = Mathf.SmoothStep(1f, 0f, Mathf.Abs(v - sourceViewportPosition.y) / 0.13f) * topGlow;
            float rayBreakup = 0.82f + 0.18f * Mathf.Sin((u * 18.0f + v * 5.0f) * Mathf.PI);

            float shape = Mathf.Max(topBand, broadGlow * 0.42f + mainShaft * 0.58f);
            float distanceFade = Mathf.Pow(vertical, falloff);
            return Mathf.Clamp01(shape * distanceFade * rayBreakup * intensity);
        }

        int ComputeHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + lightColor.GetHashCode();
                hash = hash * 31 + intensity.GetHashCode();
                hash = hash * 31 + verticalReach.GetHashCode();
                hash = hash * 31 + horizontalSpread.GetHashCode();
                hash = hash * 31 + falloff.GetHashCode();
                hash = hash * 31 + sourceViewportPosition.GetHashCode();
                hash = hash * 31 + shaftTilt.GetHashCode();
                hash = hash * 31 + shaftWidth.GetHashCode();
                hash = hash * 31 + topGlow.GetHashCode();
                hash = hash * 31 + textureWidth;
                hash = hash * 31 + textureHeight;
                return hash;
            }
        }

        void ReleaseTexture()
        {
            if (_texture == null)
                return;

            if (Application.isPlaying)
                Destroy(_texture);
            else
                DestroyImmediate(_texture);

            _texture = null;
        }
    }
}
