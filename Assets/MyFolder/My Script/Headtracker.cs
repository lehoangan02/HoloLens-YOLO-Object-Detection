using System;
using System.IO;
using UnityEngine;

public class HeadTracker : MonoBehaviour
{
    public Camera mainCamera;                   // XR Rig Main Camera
    public GameObject objectOfInterest;         // Object to track
    public GameObject hitPointDisplayPrefab;    // Prefab for visualization
    public float maxHeadDistance = 3.0f;        // Max ray length

    private GameObject hitPointDisplayer;
    private StreamWriter trackerData;

    private void Awake()
    {
        var trackerDataPath = Path.Combine(Application.persistentDataPath,
                                           "headtracking.csv");
        trackerData = new StreamWriter(trackerDataPath);
        trackerData.AutoFlush = true;
    }

    private void Start()
    {
        hitPointDisplayer = Instantiate(hitPointDisplayPrefab);
    }

    private void Update()
    {
        if (mainCamera == null) return;

        var ray = new Ray(mainCamera.transform.position,
                          mainCamera.transform.forward * maxHeadDistance);

        if (Physics.Raycast(ray, out var hit))
        {
            if (objectOfInterest == null || hit.collider.gameObject == objectOfInterest)
            {
                hitPointDisplayer.transform.position = hit.point;
                WriteTrackingPoint(hit.point);
            }
        }
    }

    private void WriteTrackingPoint(Vector3 hitPoint)
    {
        if (objectOfInterest != null)
        {
            var relativePoint = objectOfInterest.transform.position - hitPoint;
            trackerData.WriteLine(FormattableString.Invariant(
                $"{relativePoint.x},{relativePoint.y},{relativePoint.z}"));
        }
        else
        {
            trackerData.WriteLine(FormattableString.Invariant(
                $"{hitPoint.x},{hitPoint.y},{hitPoint.z}"));
        }
    }

    private void OnDestroy()
    {
        trackerData.Close();
    }
}
