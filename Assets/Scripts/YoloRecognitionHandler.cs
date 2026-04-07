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

            // Select up to MaxLabels winners: no duplicate class, no viewport overlap
            var winners = new List<DisplayedItem>();
            foreach (var candidate in candidates)
            {
                if (winners.Count >= MaxLabels) break;
                if (winners.Any(w => w.YoloItem.MostLikelyClassFood == candidate.YoloItem.MostLikelyClassFood))
                    continue;
                if (winners.Any(w => LabelsOverlapInViewport(w, candidate)))
                    continue;
                winners.Add(candidate);
            }

            // Create/update markers for all winners
            foreach (var winner in winners)
                this.ManageTrackingMarkerFood(winner);

            // Update persistent markers when there are active winners
            if (winners.Count > 0)
            {
                var newMarkers = winners
                    .Select(w => w.TrackingMarker)
                    .Where(m => m != null)
                    .ToList();

                // Destroy old persistent markers no longer in use
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
        /// Returns true if the label of <paramref name="existing"/> (already a winner with a live
        /// TrackingMarker) would visually overlap a label placed at <paramref name="candidate"/>'s
        /// world position.  Uses the existing marker's BoxCollider — which NutritionLabelController
        /// sizes to the actual canvas bounds — to derive the real screen-space footprint.
        /// Falls back to a 0.35 viewport-unit distance check when the collider isn't ready yet.
        /// </summary>
        private static bool LabelsOverlapInViewport(DisplayedItem existing, DisplayedItem candidate)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;

            // Try to read the actual world-space half-extents from the existing label's collider
            Vector2 halfExtents = Vector2.zero;
            if (existing.TrackingMarker != null)
            {
                BoxCollider col = existing.TrackingMarker.GetComponentInChildren<BoxCollider>();
                if (col != null)
                    halfExtents = new Vector2(col.size.x * 0.5f, col.size.y * 0.5f);
            }

            Vector3 posA = existing.PositionInSpace;
            Vector3 posB = candidate.PositionInSpace;

            if (halfExtents == Vector2.zero)
            {
                // Collider not ready yet — fallback: 0.35 viewport-unit distance
                Vector3 vA = cam.WorldToViewportPoint(posA);
                Vector3 vB = cam.WorldToViewportPoint(posB);
                if (vA.z < 0 || vB.z < 0) return false;
                return Vector2.Distance(new Vector2(vA.x, vA.y), new Vector2(vB.x, vB.y)) < 0.35f;
            }

            // Project the four corners of the existing label's rect to screen space,
            // then build a screen-space Rect and check if the candidate's centre falls inside
            // the union rect (expanded by the same half-extents for the candidate label).
            Vector3 right = cam.transform.right   * halfExtents.x;
            Vector3 up    = cam.transform.up      * halfExtents.y;

            // Corners of existing label in world space (label faces the camera)
            Vector3[] corners =
            {
                posA - right - up,
                posA + right - up,
                posA - right + up,
                posA + right + up,
            };

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector3 c in corners)
            {
                Vector3 sp = cam.WorldToViewportPoint(c);
                if (sp.z < 0) return false; // behind camera
                minX = Mathf.Min(minX, sp.x); maxX = Mathf.Max(maxX, sp.x);
                minY = Mathf.Min(minY, sp.y); maxY = Mathf.Max(maxY, sp.y);
            }

            // Expand by the candidate label's half-extents (same size, expressed in viewport)
            Vector3 spA = cam.WorldToViewportPoint(posA);
            Vector3 spB = cam.WorldToViewportPoint(posB);
            if (spA.z < 0 || spB.z < 0) return false;

            float vHalfW = (maxX - minX) * 0.5f;
            float vHalfH = (maxY - minY) * 0.5f;

            Rect expandedRect = new Rect(minX - vHalfW, minY - vHalfH,
                                         (maxX - minX) + 2 * vHalfW,
                                         (maxY - minY) + 2 * vHalfH);

            return expandedRect.Contains(new Vector2(spB.x, spB.y));
        }

        private void ManageTrackingMarkerFood(DisplayedItem item)
        {
            if (item.YoloItem.MostLikelyClassFood == null) return;

            if (item.TrackingMarker == null)
                item.TrackingMarker = Instantiate(this.labelObject, item.PositionInSpace, Quaternion.identity);

            NutritionLabelController labelController = item.TrackingMarker.GetComponent<NutritionLabelController>();
            if (FoodTypes.Instance == null)
            {
                Debug.LogError("FoodTypes.Instance is not initialized.");
                return;
            }

            labelController.SetInfo(FoodTypes.Instance.GetFoodItem(item.YoloItem.MostLikelyClassFood));
            labelController.UpdatePosition(item.PositionInSpace);
        }
    }
}
