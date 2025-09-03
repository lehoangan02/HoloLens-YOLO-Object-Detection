using System;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

public class HeadTracker : MonoBehaviour
{
    public Camera mainCamera;                   // XR Rig Main Camera
    public GameObject objectOfInterest;         // Object to track
    public GameObject hitPointDisplayPrefab;    // Prefab for visualization
    public float maxHeadDistance = 3.0f;        // Max ray length

    private GameObject hitPointDisplayer;
    private StreamWriter trackerData;
    private bool isTrackingEnabled = false;     // Flag to enable/disable tracking

    private void Awake()
    {
        var trackerDataPath = Path.Combine(Application.persistentDataPath, "headtracking.csv");
        trackerData = new StreamWriter(trackerDataPath);
        trackerData.AutoFlush = true;
    }

    private void Start()
    {
        hitPointDisplayer = Instantiate(hitPointDisplayPrefab);
        hitPointDisplayer.SetActive(false); // Initially disabled
    }

    private void Update()
    {
        if (!isTrackingEnabled || mainCamera == null) return;

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
        if (trackerData == null) return;

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

    public void TurnOn()
    {
        if (isTrackingEnabled) return;

        isTrackingEnabled = true;
        if (hitPointDisplayer != null)
        {
            hitPointDisplayer.SetActive(true);
        }
    }

    public void TurnOff()
    {
        if (!isTrackingEnabled) return;

        isTrackingEnabled = false;
        if (hitPointDisplayer != null)
        {
            hitPointDisplayer.SetActive(false);
        }
    }

    public string targetIP = "192.168.1.8";
    public int port = 5013;

    private void SendFileTCP()
    {
        string filePath = Path.Combine(Application.persistentDataPath, "headtracking.csv");
        using (TcpClient client = new TcpClient(targetIP, port))
        using (NetworkStream stream = client.GetStream())
        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        {
            Debug.Log("Sending file...");
            byte[] buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                stream.Write(buffer, 0, bytesRead);
            }
            Console.WriteLine("File sent.");
        }
    }

    public void SendFile()
    {
        trackerData.Close();

        try
        {
            SendFileTCP();  // may throw
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send file: {ex.Message}");
        }
        finally
        {
            // Always reopen trackerData so logging continues
            var trackerDataPath = Path.Combine(Application.persistentDataPath, "headtracking.csv");
            trackerData = new StreamWriter(trackerDataPath, append: true);
            trackerData.AutoFlush = true;
        }
    }
    public void DeleteFile()
    {
        trackerData.Close();
        try
        {
            string filePath = Path.Combine(Application.persistentDataPath, "headtracking.csv");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log("File deleted.");
            }
            else
            {
                Debug.LogWarning("File not found for deletion.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to delete file: {ex.Message}");
        }
        finally
        {
            // Always reopen trackerData so logging continues
            var trackerDataPath = Path.Combine(Application.persistentDataPath, "headtracking.csv");
            trackerData = new StreamWriter(trackerDataPath, append: true);
            trackerData.AutoFlush = true;
        }
    }
}