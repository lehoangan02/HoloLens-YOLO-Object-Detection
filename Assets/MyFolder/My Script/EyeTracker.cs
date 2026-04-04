using MixedReality.Toolkit.Input;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

public class EyeTracker : MonoBehaviour
{
    public GazeInteractor gazeInteractor;
    public GameObject     objectOfInterest;
    public GameObject     hitPointDisplayPrefab;
    public float          maxGazeDistance = 3.0f;

    private GameObject   hitPointDisplayer;
    private StreamWriter trackerData;
    private bool         isTrackingEnabled = false;

    public int port = 5012;

    private void Awake()
    {
        var trackerDataPath = Path.Combine(Application.persistentDataPath, "eyetracking.csv");
        trackerData = new StreamWriter(trackerDataPath);
        trackerData.AutoFlush = true;
    }

    private void Start()
    {
        hitPointDisplayer = Instantiate(hitPointDisplayPrefab);
        hitPointDisplayer.SetActive(false);
    }

    private void Update()
    {
        if (!isTrackingEnabled || gazeInteractor == null || objectOfInterest == null) return;

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
        if (trackerData == null) return;

        var relativePoint = objectOfInterest.transform.position - hitPoint;
        trackerData.WriteLine(FormattableString.Invariant(
            $"{relativePoint.x},{relativePoint.y},{relativePoint.z}"));
    }

    private void OnDestroy()
    {
        trackerData?.Close();
    }

    public void TurnOn()
    {
        if (isTrackingEnabled) return;
        isTrackingEnabled = true;
        hitPointDisplayer?.SetActive(true);
    }

    public void TurnOff()
    {
        if (!isTrackingEnabled) return;
        isTrackingEnabled = false;
        hitPointDisplayer?.SetActive(false);
    }

    public async void SendFile()
    {
        trackerData.Close();
        try
        {
            await SendFileTCPAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EyeTracker] Failed to send file: {ex.Message}");
        }
        finally
        {
            var trackerDataPath = Path.Combine(Application.persistentDataPath, "eyetracking.csv");
            trackerData = new StreamWriter(trackerDataPath, append: true);
            trackerData.AutoFlush = true;
        }
    }

    private async Task SendFileTCPAsync()
    {
        string targetIP = NetworkDiscovery.Instance?.MacIP;
        if (string.IsNullOrEmpty(targetIP))
        {
            Debug.LogError("[EyeTracker] Mac IP not yet discovered — cannot send file.");
            return;
        }

        string filePath = Path.Combine(Application.persistentDataPath, "eyetracking.csv");
        using (TcpClient client = new TcpClient())
        {
            await client.ConnectAsync(targetIP, port);
            using (NetworkStream stream = client.GetStream())
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                Debug.Log($"[EyeTracker] Sending file to {targetIP}:{port}...");
                byte[] buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    await stream.WriteAsync(buffer, 0, bytesRead);
                Debug.Log("[EyeTracker] File sent.");
            }
        }
    }

    public void DeleteFile()
    {
        trackerData.Close();
        var trackerDataPath = Path.Combine(Application.persistentDataPath, "eyetracking.csv");
        if (File.Exists(trackerDataPath))
        {
            File.Delete(trackerDataPath);
            Debug.Log("[EyeTracker] Deleted eyetracking.csv");
        }
        trackerData = new StreamWriter(trackerDataPath, append: true);
        trackerData.AutoFlush = true;
    }
}
