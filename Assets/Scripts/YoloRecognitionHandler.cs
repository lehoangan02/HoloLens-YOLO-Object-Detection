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

                Destroy(yoloItems[i].TrackingMarker);
                this.yoloItems.RemoveAt(i);
            }
        }

        private void TriggerDetectionActions()
        {
            // Only apply actions if item has been seen multiple times.
            var visibleItems = this.yoloItems
                .Where(item => item.IsInCameraView && item.TimesSeen >= Parameters.MinTimesSeen)
                .ToList();

            // Pick only the single most confident food item across all types
            DisplayedItem winner = visibleItems
                .Where(item => item.YoloItem.MostLikelyClassFood != null)
                .OrderByDescending(i => i.YoloItem.Confidence)
                .FirstOrDefault();

            // Show winner's label; destroy any stale markers from other items
            foreach (DisplayedItem item in this.yoloItems)
            {
                if (item == winner)
                {
                    this.ManageTrackingMarkerFood(item);
                    yoloDebugOutput.ShowDebugInformationForItem(item);
                }
                else if (item.TrackingMarker != null)
                {
                    Destroy(item.TrackingMarker);
                    item.TrackingMarker = null;
                }
            }
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
