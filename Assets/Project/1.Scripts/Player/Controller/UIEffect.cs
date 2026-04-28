using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class UIEffect
{
    private static readonly Dictionary<CanvasGroup, Coroutine> s_fadeCoroutines = new();
    private static UIEffectRunner s_runner;

    public static void ShowImmediate(GameObject target, bool disableRaycastWhenHidden = true)
    {
        if (target == null)
            return;

        CanvasGroup canvasGroup = EnsureCanvasGroup(target);
        StopFade(canvasGroup);

        target.SetActive(true);
        SetAlpha(canvasGroup, 1f, disableRaycastWhenHidden);
    }

    public static void HideImmediate(GameObject target, bool deactivate = true, bool disableRaycastWhenHidden = true)
    {
        if (target == null)
            return;

        CanvasGroup canvasGroup = EnsureCanvasGroup(target);
        StopFade(canvasGroup);

        SetAlpha(canvasGroup, 0f, disableRaycastWhenHidden);

        if (deactivate)
            target.SetActive(false);
    }

    public static void FadeIn(
        GameObject target,
        float duration,
        bool ignoreTimeScale = true,
        bool disableRaycastWhenHidden = true)
    {
        FadeTo(target, 1f, duration, false, true, ignoreTimeScale, disableRaycastWhenHidden);
    }

    public static void FadeOut(
        GameObject target,
        float duration,
        bool deactivateOnComplete = true,
        bool ignoreTimeScale = true,
        bool disableRaycastWhenHidden = true)
    {
        FadeTo(target, 0f, duration, deactivateOnComplete, true, ignoreTimeScale, disableRaycastWhenHidden);
    }

    public static void FadeTo(
        GameObject target,
        float targetAlpha,
        float duration,
        bool deactivateOnComplete = false,
        bool activateOnStart = false,
        bool ignoreTimeScale = true,
        bool disableRaycastWhenHidden = true)
    {
        if (target == null)
            return;

        CanvasGroup canvasGroup = EnsureCanvasGroup(target);
        StopFade(canvasGroup);

        if (activateOnStart)
            target.SetActive(true);

        if (duration <= 0f)
        {
            SetAlpha(canvasGroup, targetAlpha, disableRaycastWhenHidden);

            if (deactivateOnComplete && targetAlpha <= 0f)
                target.SetActive(false);

            return;
        }

        Coroutine fadeCoroutine = GetRunner().StartCoroutine(
            FadeRoutine(
                target,
                canvasGroup,
                targetAlpha,
                duration,
                deactivateOnComplete,
                ignoreTimeScale,
                disableRaycastWhenHidden));

        s_fadeCoroutines[canvasGroup] = fadeCoroutine;
    }

    public static void StopFade(GameObject target)
    {
        if (target == null)
            return;

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            return;

        StopFade(canvasGroup);
    }

    private static IEnumerator FadeRoutine(
        GameObject target,
        CanvasGroup canvasGroup,
        float targetAlpha,
        float duration,
        bool deactivateOnComplete,
        bool ignoreTimeScale,
        bool disableRaycastWhenHidden)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (target == null || canvasGroup == null)
                yield break;

            elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, normalized);
            SetAlpha(canvasGroup, alpha, disableRaycastWhenHidden);
            yield return null;
        }

        if (target != null && canvasGroup != null)
        {
            SetAlpha(canvasGroup, targetAlpha, disableRaycastWhenHidden);

            if (deactivateOnComplete && targetAlpha <= 0f)
                target.SetActive(false);

            s_fadeCoroutines.Remove(canvasGroup);
        }
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = target.AddComponent<CanvasGroup>();

        return canvasGroup;
    }

    private static void StopFade(CanvasGroup canvasGroup)
    {
        if (canvasGroup == null)
            return;

        if (!s_fadeCoroutines.TryGetValue(canvasGroup, out Coroutine fadeCoroutine))
            return;

        if (fadeCoroutine != null && s_runner != null)
            s_runner.StopCoroutine(fadeCoroutine);

        s_fadeCoroutines.Remove(canvasGroup);
    }

    private static void SetAlpha(CanvasGroup canvasGroup, float alpha, bool disableRaycastWhenHidden)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = alpha;

        if (!disableRaycastWhenHidden)
            return;

        bool isVisible = alpha > 0.001f;
        canvasGroup.interactable = isVisible;
        canvasGroup.blocksRaycasts = isVisible;
    }

    private static UIEffectRunner GetRunner()
    {
        if (s_runner != null)
            return s_runner;

        GameObject runnerObject = new GameObject("[UIEffect]");
        Object.DontDestroyOnLoad(runnerObject);
        s_runner = runnerObject.AddComponent<UIEffectRunner>();
        return s_runner;
    }

    private sealed class UIEffectRunner : MonoBehaviour
    {
    }
}
