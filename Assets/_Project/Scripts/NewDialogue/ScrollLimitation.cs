using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class LimitY : MonoBehaviour
{
    [SerializeField] private RectTransform viewport;
    [SerializeField] private float topPadding = 0f;
    [SerializeField] private float bottomPadding = 0f;
    [SerializeField, Range(0f, 1f)] private float fitContentVerticalBias = 0.72f;
    [SerializeField, Range(0f, 1f)] private float firstMessageViewportBias = 0.9f;
    [SerializeField, Range(0f, 1f)] private float latestMessageViewportBias = 0.84f;
    [SerializeField] private float delayBeforeScroll = 0.05f;
    [SerializeField] private float smoothScrollTime = 0.12f;

    private RectTransform _content;
    private ScrollRect _scrollRect;
    private Coroutine _scrollRoutine;

    private void Awake()
    {
        _content = GetComponent<RectTransform>();
        _scrollRect = GetComponentInParent<ScrollRect>();
        viewport ??= _scrollRect != null ? _scrollRect.viewport : transform.parent?.GetComponent<RectTransform>();

        if (Mathf.Approximately(fitContentVerticalBias, 0.5f))
        {
            fitContentVerticalBias = 0.72f;
        }

        AlignContentToTop();
    }

    private void OnEnable()
    {
        AlignContentToTop();
        StartCoroutine(RefreshOnNextFrame());
    }

    private void LateUpdate()
    {
        ClampContentPosition();
    }

    public void RefreshLayoutAndClamp()
    {
        AlignContentToTop();
        RebuildLayout();
        ClampContentPosition();
    }

    public void ResetScrollPosition(bool snapToLatest = false)
    {
        AlignContentToTop();
        RebuildLayout();
        SetContentY(snapToLatest ? GetMaxAllowedY() : GetMinAllowedY());
    }

    public void ScrollToLatest(bool immediate = false)
    {
        AlignContentToTop();
        RebuildLayout();

        float targetY = GetMaxAllowedY();

        if (_scrollRoutine != null)
        {
            StopCoroutine(_scrollRoutine);
            _scrollRoutine = null;
        }

        if (immediate || !isActiveAndEnabled)
        {
            SetContentY(targetY);
            return;
        }

        _scrollRoutine = StartCoroutine(SmoothScrollTo(targetY));
    }

    public void FocusOnChild(RectTransform child, bool immediate = false)
    {
        if (child == null)
        {
            ScrollToLatest(immediate);
            return;
        }

        AlignContentToTop();
        RebuildLayout();

        bool isFirstMessage = _content != null && _content.childCount <= 1;
        float targetBias = isFirstMessage ? firstMessageViewportBias : latestMessageViewportBias;
        float targetY = GetTargetYForChild(child, targetBias);

        if (_scrollRoutine != null)
        {
            StopCoroutine(_scrollRoutine);
            _scrollRoutine = null;
        }

        if (immediate || !isActiveAndEnabled)
        {
            SetContentY(targetY);
            return;
        }

        _scrollRoutine = StartCoroutine(SmoothScrollTo(targetY));
    }

    private IEnumerator RefreshOnNextFrame()
    {
        yield return null;
        RefreshLayoutAndClamp();
    }

    private IEnumerator SmoothScrollTo(float targetY)
    {
        if (delayBeforeScroll > 0f)
        {
            yield return new WaitForSecondsRealtime(delayBeforeScroll);
        }

        float velocity = 0f;
        float elapsed = 0f;
        const float maxDuration = 0.35f;

        while (_content != null && elapsed < maxDuration)
        {
            float nextY = Mathf.SmoothDamp(
                _content.anchoredPosition.y,
                targetY,
                ref velocity,
                smoothScrollTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            SetContentY(nextY);

            if (Mathf.Abs(_content.anchoredPosition.y - targetY) <= 0.5f)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        SetContentY(targetY);
        _scrollRoutine = null;
    }

    private void AlignContentToTop()
    {
        if (_content == null)
        {
            return;
        }

        if (Mathf.Approximately(_content.anchorMin.y, 1f) &&
            Mathf.Approximately(_content.anchorMax.y, 1f) &&
            Mathf.Approximately(_content.pivot.y, 1f))
        {
            return;
        }

        Vector2 anchoredPosition = _content.anchoredPosition;
        _content.anchorMin = new Vector2(_content.anchorMin.x, 1f);
        _content.anchorMax = new Vector2(_content.anchorMax.x, 1f);
        _content.pivot = new Vector2(_content.pivot.x, 1f);
        _content.anchoredPosition = anchoredPosition;
    }

    private void RebuildLayout()
    {
        if (_content == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);

        if (viewport != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
        }
    }

    private void ClampContentPosition()
    {
        if (_content == null || viewport == null)
        {
            return;
        }

        SetContentY(_content.anchoredPosition.y);
    }

    private void SetContentY(float targetY)
    {
        if (_content == null)
        {
            return;
        }

        Vector2 position = _content.anchoredPosition;
        position.y = Mathf.Clamp(targetY, GetMinAllowedY(), GetMaxAllowedY());
        _content.anchoredPosition = position;
    }

    private float GetMinAllowedY()
    {
        if (_content == null || viewport == null)
        {
            return -topPadding;
        }

        return ContentFitsViewport() ? GetFitContentY() : -topPadding;
    }

    private float GetMaxAllowedY()
    {
        if (_content == null || viewport == null)
        {
            return -topPadding;
        }

        if (ContentFitsViewport())
        {
            return GetFitContentY();
        }

        float contentHeight = _content.rect.height;
        float viewportHeight = viewport.rect.height;
        return Mathf.Max(-topPadding, contentHeight - viewportHeight + bottomPadding);
    }

    private bool ContentFitsViewport()
    {
        if (_content == null || viewport == null)
        {
            return true;
        }

        return _content.rect.height <= viewport.rect.height;
    }

    private float GetFitContentY()
    {
        if (_content == null || viewport == null)
        {
            return -topPadding;
        }

        float freeSpace = Mathf.Max(0f, viewport.rect.height - _content.rect.height);
        float preferredOffset = Mathf.Max(topPadding, freeSpace * fitContentVerticalBias);
        float maxOffset = Mathf.Max(topPadding, freeSpace - bottomPadding);
        float offset = Mathf.Min(preferredOffset, maxOffset);
        return -offset;
    }

    private float GetTargetYForChild(RectTransform child, float viewportBias)
    {
        if (_content == null || viewport == null || child == null)
        {
            return GetMaxAllowedY();
        }

        Vector3 childCenterInViewport = viewport.InverseTransformPoint(child.TransformPoint(child.rect.center));
        float desiredY = Mathf.Lerp(viewport.rect.yMax - topPadding, viewport.rect.yMin + bottomPadding, viewportBias);
        float delta = desiredY - childCenterInViewport.y;
        return _content.anchoredPosition.y + delta;
    }
}
