using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace Game.MiniGame.Agility
{
    public class SliderTimerManager : MonoBehaviour
    {
        [SerializeField] private Slider timerSlider;
        [SerializeField] private Image committedFillImage;
        [SerializeField] private RectTransform[] dividerMarkers;
        private float gameDuration;
        private int segmentCount = 1;
        private readonly List<float> dividerStops = new();

        public void InitializeTimer(float duration, int segments = 1)
        {
            gameDuration = duration;
            segmentCount = Mathf.Max(1, segments);
            EnsureCommittedFillResolved();
            RefreshDividerStops(segmentCount);

            ConfigureSlider(timerSlider);
            ConfigureFillImage(committedFillImage);
            SetFillAmount(committedFillImage, 0f);
            SetSliderValue(timerSlider, 0f);
        }

        public void SetSegmentProgress(int segmentIndex, int totalSegments, float normalizedInSegment)
        {
            if (timerSlider == null)
                return;

            segmentCount = Mathf.Max(1, totalSegments);
            float clampedSegmentProgress = Mathf.Clamp01(normalizedInSegment);
            float overallProgress = ResolveOverallProgress(segmentIndex, clampedSegmentProgress, segmentCount);

            SetFillAmount(committedFillImage, overallProgress);
            SetSliderValue(timerSlider, Mathf.Clamp01(overallProgress));
        }

        public void SnapToDivider(int completedSegments, int totalSegments)
        {
            if (timerSlider == null)
                return;

            segmentCount = Mathf.Max(1, totalSegments);
            RefreshDividerStops(segmentCount);
            int stopIndex = Mathf.Clamp(completedSegments, 0, dividerStops.Count - 1);
            float overallProgress = dividerStops[stopIndex];
            SetFillAmount(committedFillImage, overallProgress);
            SetSliderValue(timerSlider, overallProgress);
        }

        public void SetRemainingTime(float remainingTime, float totalDuration = -1f)
        {
            if (timerSlider == null)
                return;

            if (totalDuration > 0f && !Mathf.Approximately(totalDuration, gameDuration))
            {
                gameDuration = totalDuration;
                ConfigureSlider(timerSlider);
                ConfigureFillImage(committedFillImage);
            }

            float elapsed = Mathf.Clamp(gameDuration - remainingTime, 0f, gameDuration);
            float normalized = gameDuration > 0f ? Mathf.Clamp01(elapsed / gameDuration) : 0f;
            SetFillAmount(committedFillImage, normalized);
            SetSliderValue(timerSlider, normalized);
        }

        public void UpdateTimer(float remainingTime)
        {
            SetRemainingTime(remainingTime);
        }

        public void ResetTimer()
        {
            SetFillAmount(committedFillImage, 0f);
            SetSliderValue(timerSlider, 0f);
        }

        private float ResolveOverallProgress(int segmentIndex, float normalizedInSegment, int totalSegments)
        {
            RefreshDividerStops(totalSegments);

            if (dividerStops.Count < 2)
                return 0f;

            int clampedIndex = Mathf.Clamp(segmentIndex, 0, dividerStops.Count - 2);
            float start = dividerStops[clampedIndex];
            float end = dividerStops[clampedIndex + 1];
            return Mathf.Lerp(start, end, normalizedInSegment);
        }

        private void RefreshDividerStops(int totalSegments)
        {
            dividerStops.Clear();
            dividerStops.Add(0f);

            List<float> markerStops = CollectMarkerStops();
            int expectedDividerCount = Mathf.Max(0, totalSegments - 1);

            if (markerStops.Count == expectedDividerCount)
            {
                dividerStops.AddRange(markerStops);
            }
            else
            {
                for (int i = 1; i < totalSegments; i++)
                    dividerStops.Add(i / (float)totalSegments);
            }

            dividerStops.Add(1f);
        }

        private List<float> CollectMarkerStops()
        {
            var stops = new List<float>();
            EnsureDividerMarkersResolved();

            if (timerSlider == null)
                return stops;

            RectTransform sliderRect = timerSlider.GetComponent<RectTransform>();
            if (sliderRect == null)
                return stops;

            for (int i = 0; i < dividerMarkers.Length; i++)
            {
                RectTransform marker = dividerMarkers[i];
                if (marker == null)
                    continue;

                Vector3 worldCenter = marker.TransformPoint(marker.rect.center);
                Vector3 localCenter = sliderRect.InverseTransformPoint(worldCenter);
                float normalized = Mathf.InverseLerp(sliderRect.rect.xMin, sliderRect.rect.xMax, localCenter.x);
                stops.Add(Mathf.Clamp01(normalized));
            }

            stops.Sort();
            return stops;
        }

        private void EnsureDividerMarkersResolved()
        {
            if (dividerMarkers != null && dividerMarkers.Length > 0)
                return;

            if (timerSlider == null)
            {
                dividerMarkers = Array.Empty<RectTransform>();
                return;
            }

            Canvas canvas = timerSlider.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                dividerMarkers = Array.Empty<RectTransform>();
                return;
            }

            var foundMarkers = new List<RectTransform>();
            RectTransform[] rectTransforms = canvas.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rectTransforms.Length; i++)
            {
                RectTransform rectTransform = rectTransforms[i];
                if (rectTransform == null)
                    continue;

                if (string.Equals(rectTransform.name, "Sword0", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rectTransform.name, "Sword1", StringComparison.OrdinalIgnoreCase))
                {
                    foundMarkers.Add(rectTransform);
                }
            }

            foundMarkers.Sort((left, right) => left.position.x.CompareTo(right.position.x));
            dividerMarkers = foundMarkers.ToArray();
        }

        private void EnsureCommittedFillResolved()
        {
            if (timerSlider == null || committedFillImage != null)
                return;

            Transform searchRoot = timerSlider.transform.parent != null ? timerSlider.transform.parent : timerSlider.transform;
            RectTransform sliderFillRect = timerSlider.fillRect;
            Image[] images = searchRoot.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image candidate = images[i];
                if (candidate == null)
                    continue;

                RectTransform candidateRect = candidate.rectTransform;
                if (sliderFillRect != null && candidateRect == sliderFillRect)
                    continue;

                string imageName = candidate.name;
                if (string.Equals(imageName, "CommittedFill", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(imageName, "Filled", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(imageName, "FilledFill", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(imageName, "CompletedFill", StringComparison.OrdinalIgnoreCase))
                {
                    committedFillImage = candidate;
                    break;
                }
            }
        }

        private static void ConfigureSlider(Slider slider)
        {
            if (slider == null)
                return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
        }

        private static void ConfigureFillImage(Image image)
        {
            if (image == null)
                return;

            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        private static void SetSliderValue(Slider slider, float value)
        {
            if (slider == null)
                return;

            slider.value = Mathf.Clamp01(value);
        }

        private static void SetFillAmount(Image image, float value)
        {
            if (image == null)
                return;

            image.fillAmount = Mathf.Clamp01(value);
        }
    }
}
