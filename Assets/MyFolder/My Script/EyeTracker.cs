using MixedReality.Toolkit.Input;
using System;
using System.IO;
using UnityEngine;

public class EyeTracker : MonoBehaviour
{
    public GazeInteractor gazeInteractor;         // From MRTK3
    public GameObject objectOfInterest;           // The target object
    public GameObject hitPointDisplayPrefab;      // Prefab to show gaze hit point
    public float maxGazeDistance = 3.0f;          // Max distance for gaze ray

    private GameObject hitPointDisplayer;
    private StreamWriter trackerData;

    private void Awake()
    {
        var trackerDataPath = Path.Combine(Application.persistentDataPath,
                                           "eyetracking.csv");
        trackerData = new StreamWriter(trackerDataPath);
        trackerData.AutoFlush = true;
    }

    private void Start()
    {
        hitPointDisplayer = Instantiate(hitPointDisplayPrefab);
    }

    private void Update()
    {
        if (gazeInteractor == null || objectOfInterest == null) return;

        var ray = new Ray(gazeInteractor.rayOriginTransform.position,
                          gazeInteractor.rayOriginTransform.forward * maxGazeDistance);

        if (Physics.Raycast(ray, out var hit))
        {
            if (hit.collider.gameObject == objectOfInterest)
            {
                hitPointDisplayer.transform.position = hit.point;
                WriteTrackingPoint(hit.point);
            }
        }
    }

    private void WriteTrackingPoint(Vector3 hitPoint)
    {
        var relativePoint = objectOfInterest.transform.position - hitPoint;
        trackerData.WriteLine(FormattableString.Invariant(
            $"{relativePoint.x},{relativePoint.y},{relativePoint.z}"));
    }

    private void OnDestroy()
    {
        trackerData.Close();
    }
}
