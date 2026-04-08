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
                // Food items from the Mac receiver always have MostLikelyClass = default(0),
                // so match by food name string instead to avoid cross-class merging.
                bool sameClass = !string.IsNullOrEmpty(item.MostLikelyClassFood)
                    ? oldItem.YoloItem.MostLikelyClassFood == item.MostLikelyClassFood
                    : oldItem.YoloItem.MostLikelyClass.Equals(item.MostLikelyClass);

                if (!sameClass) continue;

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

            // Select up to MaxLabels winners: no duplicate display name.
            // Bun-family items are remapped to display as Pho in FoodTypes, so the display-name
            // dedup automatically prevents two "Pho" labels (e.g. real pho + bun bo hue).
            // Overlap/depth is handled by nudging display positions, not by dropping labels.
            var winners = new List<DisplayedItem>();
            foreach (var candidate in candidates)
            {
                if (winners.Count >= MaxLabels) break;

                // Deduplicate by display name (what FoodTypes resolves to), not raw YOLO class.
                string candidateDisplay = GetDisplayName(candidate.YoloItem.MostLikelyClassFood);
                if (winners.Any(w => GetDisplayName(w.YoloItem.MostLikelyClassFood) == candidateDisplay))
                    continue;

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
        /// axis when their label rects would overlap in viewport space OR when one food is 0.6 m+
        /// behind the other (depth covering — even if labels just barely clear each other, the
        /// depth gap makes the scene confusing so we force extra separation).
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

            // Get the label's world-space half-width from the collider of whichever marker exists.
            // Use the average depth of both positions to get a representative viewport scale.
            float halfWidthWorld = GetLabelHalfWidth(winners[0].TrackingMarker);
            if (halfWidthWorld == 0f) halfWidthWorld = GetLabelHalfWidth(winners[1].TrackingMarker);
            if (halfWidthWorld == 0f) halfWidthWorld = 0.15f; // fallback until collider is ready

            Vector3 midPos = (posA + posB) * 0.5f;
            Vector3 vMid   = cam.WorldToViewportPoint(midPos);
            float labelVpHalfWidth = Mathf.Abs(
                cam.WorldToViewportPoint(midPos + cam.transform.right * halfWidthWorld).x - vMid.x);

            // Two full label widths + small gap = minimum centre-to-centre separation needed
            float requiredSep = 2f * labelVpHalfWidth + 0.02f;

            // Nudge if labels overlap on screen, OR if one food is 0.6 m+ behind the other
            // (depth covering: the front label can obscure the back food even if rects barely clear).
            bool needsNudge = screenDist2D < requiredSep || depthDiff >= 0.6f;
            if (!needsNudge) return positions;

            // When depth covering forces a nudge despite labels already being screen-separated,
            // use the full required separation as the target instead of the current distance.
            float targetSep = requiredSep;
            float deficit   = targetSep - screenDist2D;
            if (deficit <= 0f) return positions;

            // Determine push direction for B (A goes the opposite way); if centres coincide, push right
            float dirB = (vB.x >= vA.x) ? 1f : -1f;
            if (Mathf.Abs(vB.x - vA.x) < 0.001f) dirB = 1f;

            float halfDeficit = deficit * 0.5f;

            // Compute viewport-x per world-metre at each position by sampling a small offset
            const float testWorld = 0.05f;
            float vpPerWorldA = (cam.WorldToViewportPoint(posA + cam.transform.right * testWorld).x - vA.x) / testWorld;
            float vpPerWorldB = (cam.WorldToViewportPoint(posB + cam.transform.right * testWorld).x - vB.x) / testWorld;

            // Guard against degenerate cases (labels extremely far away or behind cam)
            if (Mathf.Abs(vpPerWorldA) < 0.0001f || Mathf.Abs(vpPerWorldB) < 0.0001f)
                return positions;

            // Push A and B apart symmetrically along camera right
            positions[0] = posA + cam.transform.right * (-dirB * halfDeficit / vpPerWorldA);
            positions[1] = posB + cam.transform.right * ( dirB * halfDeficit / vpPerWorldB);

            return positions;
        }

        /// <summary>
        /// Returns the display name FoodTypes would show for a given raw YOLO class name.
        /// Falls back to the raw name if FoodTypes is not yet initialised or the key is unknown.
        /// </summary>
        private static string GetDisplayName(string rawClass)
        {
            if (string.IsNullOrEmpty(rawClass)) return rawClass;
            if (FoodTypes.Instance == null) return rawClass;
            FoodItem item = FoodTypes.Instance.GetFoodItem(rawClass);
            return item != null ? item.Name : rawClass;
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
