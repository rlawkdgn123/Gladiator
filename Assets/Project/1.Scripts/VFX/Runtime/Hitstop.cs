using UnityEngine;

namespace Game.VFX.Runtime
{
    /// <summary>
    /// 타격감 강화를 위한 전역 Time.timeScale 일시 저하.
    /// 싱글톤. 중복 요청은 (남은시간, 강도) 중 "더 강한 쪽"으로 갱신.
    /// UI/사운드가 unscaledDeltaTime을 써야 정지감 없이 자연스러움.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class Hitstop : MonoBehaviour
    {
        static Hitstop _instance;

        float _remaining;
        float _activeScale = 1f;
        float _originalScale = 1f;
        bool _active;

        public static void Request(float duration, float scale = 0.08f)
        {
            EnsureInstance();
            _instance.RequestInternal(duration, scale);
        }

        public static bool IsActive => _instance != null && _instance._active;

        static void EnsureInstance()
        {
            if (_instance != null) return;

            var go = new GameObject("[Hitstop]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<Hitstop>();
        }

        void RequestInternal(float duration, float scale)
        {
            if (duration <= 0f) return;

            if (!_active)
            {
                _originalScale = Time.timeScale;
                _active = true;
            }

            // 더 강하거나 더 긴 요청으로 "업그레이드"만 허용
            if (scale < _activeScale) _activeScale = scale;
            if (duration > _remaining) _remaining = duration;

            Time.timeScale = _activeScale;
        }

        void Update()
        {
            if (!_active) return;

            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f)
            {
                Time.timeScale = _originalScale;
                _active = false;
                _activeScale = 1f;
            }
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                if (_active) Time.timeScale = _originalScale;
                _instance = null;
            }
        }
    }
}
