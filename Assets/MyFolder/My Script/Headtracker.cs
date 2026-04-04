using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

public class HeadTracker : MonoBehaviour
{
    public Camera     mainCamera;
    public GameObject objectOfInterest;
    public GameObject hitPointDisplayPrefab;
    public float      maxHeadDistance = 3.0f;

    private GameObject   hitPointDisplayer;
    private StreamWriter trackerData;
    private bool         isTrackingEnabled = false;
    private int          _writeCount       = 0;

    public int port = 5013;

    private void Awake()
    {
        var trackerDataPath = Path.Combine(Application.persistentDataPath, "headtracking.csv");
        trackerData = new StreamWriter(trackerDataPath);
        trackerData.AutoFlush = false;
        trackerData.WriteLine("timestamp_utc,rel_x,rel_y,rel_z");
    }

    private void Start()
    {
        hitPointDisplayer = Instantiate(hitPointDisplayPrefab);
        hitPointDisplayer.SetActive(false);
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

        string ts = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        if (objectOfInterest != null)
        {
            var relativePoint = objectOfInterest.transform.position - hitPoint;
            trackerData.WriteLine(FormattableString.Invariant(
                $"{ts},{relativePoint.x},{relativePoint.y},{relativePoint.z}"));
        }
        else
        {
            trackerData.WriteLine(FormattableString.Invariant(
                $"{ts},{hitPoint.x},{hitPoint.y},{hitPoint.z}"));
        }
        if (++_writeCount % 30 == 0) trackerData.Flush();
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
            Debug.LogError($"[HeadTracker] Failed to send file: {ex.Message}");
        }
        finally
        {
            var trackerDataPath = Path.Combine(Application.persistentDataPath, "headtracking.csv");
            trackerData = new StreamWriter(trackerDataPath, append: true);
            trackerData.AutoFlush = false;
        }
    }

    private async Task SendFileTCPAsync()
    {
        // Wait up to 5 s for MacIP if not yet discovered
        string targetIP = NetworkDiscovery.Instance?.MacIP;
        if (string.IsNullOrEmpty(targetIP))
        {
            Debug.LogWarning("[HeadTracker] MacIP not yet known — waiting up to 5 s…");
            for (int i = 0; i < 50 && string.IsNullOrEmpty(targetIP); i++)
            {
                await Task.Delay(100);
                targetIP = NetworkDiscovery.Instance?.MacIP;
            }
        }
        if (string.IsNullOrEmpty(targetIP))
        {
            Debug.LogError("[HeadTracker] Mac IP not discovered after 5 s — cannot send file.");
            return;
        }

        string filePath = Path.Combine(Application.persistentDataPath, "headtracking.csv");
        using (TcpClient client = new TcpClient())
        {
            await client.ConnectAsync(targetIP, port);
            using (NetworkStream stream = client.GetStream())
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                Debug.Log($"[HeadTracker] Sending file to {targetIP}:{port}...");
                byte[] buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    await stream.WriteAsync(buffer, 0, bytesRead);
                Debug.Log("[HeadTracker] File sent.");
            }
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
                Debug.Log("[HeadTracker] File deleted.");
            }
            else
            {
                Debug.LogWarning("[HeadTracker] File not found for deletion.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HeadTracker] Failed to delete file: {ex.Message}");
        }
        finally
        {
            var trackerDataPath = Path.Combine(Application.persistentDataPath, "headtracking.csv");
            trackerData = new StreamWriter(trackerDataPath, append: false);
            trackerData.AutoFlush = false;
            trackerData.WriteLine("timestamp_utc,rel_x,rel_y,rel_z");
        }
    }
}
