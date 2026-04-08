using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts
{
    /// <summary>
    ///     Handles the recognitions of the yolo model.
    /// </summary>
    public class YoloRecognitionHandler : MonoBehaviour
    {
        private readonly List<DisplayedItem> yoloItems = new();
        private readonly List<GameObject> _persistentMarkers = new();
        private const int MaxLabels = 2;

        // When Pho is already shown, these classes are suppressed as the second label —
        // they co-occur with pho in real life and would mask the actual second food (banh mi).
        private static readonly HashSet<string> PhoCoOccurrenceExclusions = new()
        {
            "Bun bo Hue (Hue beef noodle soup)",
            "Bun cha (Grilled pork with vermicelli)",
            "Bun dau (Vermicelli with tofu)",
            "Bun mam (Fermented fish noodle soup)",
            "Bun rieu (Crab noodle soup)",
        };

        [SerializeField]
        private GameObject labelObject;

        private YoloDebugOutput yoloDebugOutput;

        private void Start()
        {
            this.yoloDebugOutput = gameObject.GetComponent<YoloDebugOutput>();
        }

        /// <summary>
        ///     Post process the recognitions and show them.
        /// </summary>
        /// <param name="recognitions">Recognitions of the model.</param>
        /// <param name="cameraTransform">The current camera position.</param>
        public void ShowRecognitions(List<YoloItem> recognitions, CameraTransform cameraTransform)
        {
            this.AddNewlyRecognizedObjects(recognitions, cameraTransform);
            this.RemoveOutdatedObjects();
            this.TriggerDetectionActions();
        }

        private void AddNewlyRecognizedObjects(List<YoloItem> recognitions, CameraTransform cameraTransform)
        {
            List<DisplayedItem> unmatchedExistingItems = new(this.yoloItems);
            foreach (YoloItem newItem in recognitions)
            {
                Vector3? positionInSpace = PositionCalculator.CalculatePointInSpace(newItem, cameraTransform);
                if (positionInSpace == null) continue;

                DisplayedItem item = this.GetClosestExistingItem(unmatchedExistingItems, newItem, positionInSpace.Value);
                if (item == null)
                {
                    item = new DisplayedItem(newItem, positionInSpace.Value);
                    this.yoloItems.Add(item);
                }
                else
                {
                    unmatchedExistingItems.Remove(item);
                    item.UpdateItem(newItem, positionInSpace.Value);
                }
            }
        }

        private DisplayedItem GetClosestExistingItem(List<DisplayedItem> oldItems, YoloItem item, Vector3 positionInSpace)
        {
            DisplayedItem closestItem = null;
            float closestDist = float.MaxValue;

            foreach (DisplayedItem oldItem in oldItems)
            {
                if (!oldItem.YoloItem.MostLikelyClass.Equals(item.MostLikelyClass)) continue;

                float distance = Vector3.Distance(oldItem.PositionInSpace, positionInSpace);
                if (distance > Parameters.MaxIdenticalObject || distance >= closestDist) continue;

                closestItem = oldItem;
                closestDist = distance;
            }

            return closestItem;
        }

        private void RemoveOutdatedObjects()
        {
            for (int i = this.yoloItems.Count - 1; i >= 0; i--)
            {
                bool wasInCameraView = this.yoloItems[i].IsInCameraView;
                bool isInCameraView = PositionCalculator.IsObjectInCameraView(this.yoloItems[i].PositionInSpace);
                this.yoloItems[i].IsInCameraView = isInCameraView;

                if (!isInCameraView) continue;

                if (!wasInCameraView)
                {
                    this.yoloItems[i].TimeLastSeen = Time.time;
                    continue;
                }

                if (Time.time - this.yoloItems[i].TimeLastSeen <= Parameters.ObjectTimeOut) continue;

                // Preserve persistent markers — they stay visible until replaced
                if (!_persistentMarkers.Contains(yoloItems[i].TrackingMarker))
                    Destroy(yoloItems[i].TrackingMarker);
                yoloItems[i].TrackingMarker = null;
                this.yoloItems.RemoveAt(i);
            }
        }

        private void TriggerDetectionActions()
        {
            var visibleItems = this.yoloItems
                .Where(item => item.IsInCameraView && item.TimesSeen >= Parameters.MinTimesSeen)
                .ToList();

            // Candidates: valid food, sorted by confidence desc
            var candidates = visibleItems
                .Where(item => item.YoloItem.MostLikelyClassFood != null
                            && item.YoloItem.MostLikelyClassFood != "Pho mai")
                .OrderByDescending(i => i.YoloItem.Confidence)
                .ToList();

            // Select up to MaxLabels winners: no duplicate class only
            // Overlap/depth is handled by nudging display positions, not by dropping labels
            var winners = new List<DisplayedItem>();
            foreach (var candidate in candidates)
            {
                if (winners.Count >= MaxLabels) break;
                if (winners.Any(w => w.YoloItem.MostLikelyClassFood == candidate.YoloItem.MostLikelyClassFood))
                    continue;
                // Suppress bun-family classes whenever pho is involved (either slot)
                bool phoAlreadyWon = winners.Any(w => w.YoloItem.MostLikelyClassFood == "Pho (Vietnamese noodle soup)");
                bool candidateIsPho = candidate.YoloItem.MostLikelyClassFood == "Pho (Vietnamese noodle soup)";
                bool bunFamilyAlreadyWon = winners.Any(w => PhoCoOccurrenceExclusions.Contains(w.YoloItem.MostLikelyClassFood));
                if (phoAlreadyWon && PhoCoOccurrenceExclusions.Contains(candidate.YoloItem.MostLikelyClassFood))
                    continue;
                if (candidateIsPho && bunFamilyAlreadyWon)
                {
                    // Replace the bun-family slot with pho, then keep looking for banh mi
                    int bunIdx = winners.FindIndex(w => PhoCoOccurrenceExclusions.Contains(w.YoloItem.MostLikelyClassFood));
                    winners[bunIdx] = candidate;
                    continue;
                }
                winners.Add(candidate);
            }

            // Compute display positions — nudge apart if they'd overlap or one hides behind the other
            Vector3[] displayPositions = ComputeDisplayPositions(winners);

            // Create/update markers at their (possibly nudged) display positions
            for (int i = 0; i < winners.Count; i++)
                this.ManageTrackingMarkerFood(winners[i], displayPositions[i]);

            // Update persistent markers when there are active winners
            if (winners.Count > 0)
            {
                var newMarkers = winners
                    .Select(w => w.TrackingMarker)
                    .Where(m => m != null)
                    .ToList();

                foreach (var pm in _persistentMarkers)
                {
                    if (pm != null && !newMarkers.Contains(pm))
                        Destroy(pm);
                }
                _persistentMarkers.Clear();
                _persistentMarkers.AddRange(newMarkers);

                yoloDebugOutput?.ShowDebugInformationForItem(winners[0]);
            }
            // When no winners, persistent markers stay visible at their last positions

            // Destroy markers of non-winner items, but never persistent markers
            foreach (DisplayedItem item in this.yoloItems)
            {
                if (winners.Contains(item)) continue;
                if (item.TrackingMarker != null && !_persistentMarkers.Contains(item.TrackingMarker))
                {
                    Destroy(item.TrackingMarker);
                    item.TrackingMarker = null;
                }
            }
        }

        /// <summary>
        /// Returns display positions for each winner, nudging them apart along the camera's right
        /// axis when either:
        ///   - their label rects would overlap in viewport space, or
        ///   - one is 0.6 m+ behind the other and close on screen (depth covering).
        /// The nudge is split symmetrically: both labels move equal amounts in opposite directions.
        /// </summary>
        private static Vector3[] ComputeDisplayPositions(List<DisplayedItem> winners)
        {
            var positions = winners.Select(w => w.PositionInSpace).ToArray();
            if (winners.Count < 2) return positions;

            Camera cam = Camera.main;
            if (cam == null) return positions;

            Vector3 posA = positions[0];
            Vector3 posB = positions[1];

            Vector3 vA = cam.WorldToViewportPoint(posA);
            Vector3 vB = cam.WorldToViewportPoint(posB);

            // If either point is behind the camera we can't compute reliable viewport coords
            if (vA.z < 0 || vB.z < 0) return positions;

            float screenDist2D = Vector2.Distance(new Vector2(vA.x, vA.y), new Vector2(vB.x, vB.y));
            float depthDiff    = Mathf.Abs(vA.z - vB.z);

            // Get the label's world-space half-width from the collider of whichever marker exists
            float halfWidthWorld = GetLabelHalfWidth(winners[0].TrackingMarker);
            if (halfWidthWorld == 0f) halfWidthWorld = GetLabelHalfWidth(winners[1].TrackingMarker);
            if (halfWidthWorld == 0f) halfWidthWorld = 0.15f; // fallback until collider is ready

            // Convert half-width to viewport units at posA's depth
            float labelVpHalfWidth = Mathf.Abs(
                cam.WorldToViewportPoint(posA + cam.transform.right * halfWidthWorld).x - vA.x);

            // Two label widths + small margin = minimum centre-to-centre separation needed
            float requiredSep = 2f * labelVpHalfWidth + 0.02f;

            // Check both overlap cases
            bool screenOverlap  = screenDist2D < requiredSep;
            bool depthCovering  = depthDiff >= 0.6f && screenDist2D < requiredSep;

            if (!screenOverlap && !depthCovering) return positions;

            float deficit = requiredSep - screenDist2D;
            if (deficit <= 0f) return positions;

            // Determine push direction for B (A goes the opposite way); if centres coincide, push right
            float dirB = (vB.x >= vA.x) ? 1f : -1f;
            if (Mathf.Abs(vB.x - vA.x) < 0.001f) dirB = 1f;

            float halfDeficit = deficit * 0.5f;

            // Compute world-to-viewport-x scale at each position by sampling a small offset
            const float testWorld = 0.05f;
            float vpPerWorldA = (cam.WorldToViewportPoint(posA + cam.transform.right * testWorld).x - vA.x) / testWorld;
            float vpPerWorldB = (cam.WorldToViewportPoint(posB + cam.transform.right * testWorld).x - vB.x) / testWorld;

            // Guard against degenerate cases (labels extremely far away)
            if (Mathf.Abs(vpPerWorldA) < 0.0001f || Mathf.Abs(vpPerWorldB) < 0.0001f)
                return positions;

            // Push A and B apart symmetrically along camera right
            positions[0] = posA + cam.transform.right * (-dirB * halfDeficit / vpPerWorldA);
            positions[1] = posB + cam.transform.right * ( dirB * halfDeficit / vpPerWorldB);

            return positions;
        }

        private static float GetLabelHalfWidth(GameObject marker)
        {
            if (marker == null) return 0f;
            BoxCollider col = marker.GetComponentInChildren<BoxCollider>();
            return col != null ? col.size.x * 0.5f : 0f;
        }

        private void ManageTrackingMarkerFood(DisplayedItem item, Vector3 displayPosition)
        {
            if (item.YoloItem.MostLikelyClassFood == null) return;

            if (item.TrackingMarker == null)
                item.TrackingMarker = Instantiate(this.labelObject, displayPosition, Quaternion.identity);

            NutritionLabelController labelController = item.TrackingMarker.GetComponent<NutritionLabelController>();
            if (FoodTypes.Instance == null)
            {
                Debug.LogError("FoodTypes.Instance is not initialized.");
                return;
            }

            labelController.SetInfo(FoodTypes.Instance.GetFoodItem(item.YoloItem.MostLikelyClassFood));
            labelController.UpdatePosition(displayPosition);
        }
    }
}
