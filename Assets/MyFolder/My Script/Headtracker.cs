using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

public class HeadTracker : MonoBehaviour
{
    public Camera     mainCamera;
    public GameObject objectOfInterest;      // optional
    public GameObject hitPointDisplayPrefab; // optional
    public float      maxHeadDistance = 3.0f;

    private GameObject   hitPointDisplayer;
    private StreamWriter trackerData;
    private bool         isTrackingEnabled = false;
    private int          _writeCount       = 0;

    public int port = 5013;

    private void Awake()
    {
        var path = Path.Combine(Application.persistentDataPath, "headtracking.csv");
        trackerData = new StreamWriter(path);
        trackerData.AutoFlush = false;
        trackerData.WriteLine("timestamp_utc,rel_x,rel_y,rel_z");
    }

    private void Start()
    {
        // Auto-resolve camera if not assigned in Inspector
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Guard: only instantiate if prefab is assigned
        if (hitPointDisplayPrefab != null)
        {
            hitPointDisplayer = Instantiate(hitPointDisplayPrefab);
            hitPointDisplayer.SetActive(false);
        }
    }

    private void Update()
    {
        // Re-resolve in case Camera.main changed (e.g. scene reload)
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (!isTrackingEnabled || mainCamera == null) return;

        var ray = new Ray(mainCamera.transform.position,
                          mainCamera.transform.forward);

        if (!TryFindTrackingHit(ray, out RaycastHit hit)) return;

        // Only act when head ray hits the target object (or any object if none specified)
        if (objectOfInterest != null && hit.collider.gameObject != objectOfInterest) return;

        if (hitPointDisplayer != null)
            hitPointDisplayer.transform.position = hit.point;

        WriteTrackingPoint(hit.point);
    }

    private void WriteTrackingPoint(Vector3 hitPoint)
    {
        if (trackerData == null) return;

        string ts = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        if (objectOfInterest != null)
        {
            var rel = objectOfInterest.transform.position - hitPoint;
            trackerData.WriteLine(FormattableString.Invariant(
                $"{ts},{rel.x},{rel.y},{rel.z}"));
        }
        else
        {
            trackerData.WriteLine(FormattableString.Invariant(
                $"{ts},{hitPoint.x},{hitPoint.y},{hitPoint.z}"));
        }
        if (++_writeCount % 30 == 0) trackerData.Flush();
    }

    private bool TryFindTrackingHit(Ray ray, out RaycastHit selectedHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            maxHeadDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<NutritionLabelController>() != null)
                continue;

            if (objectOfInterest != null && hit.collider.gameObject != objectOfInterest)
                continue;

            selectedHit = hit;
            return true;
        }

        selectedHit = default;
        return false;
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
        Debug.Log("[HeadTracker] Tracking ON");
    }

    public void TurnOff()
    {
        if (!isTrackingEnabled) return;
        isTrackingEnabled = false;
        hitPointDisplayer?.SetActive(false);
        Debug.Log("[HeadTracker] Tracking OFF");
    }

    public async void SendFile()
    {
        trackerData?.Flush();
        trackerData?.Close();
        trackerData = null;
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
            var path = Path.Combine(Application.persistentDataPath, "headtracking.csv");
            trackerData = new StreamWriter(path, append: true);
            trackerData.AutoFlush = false;
        }
    }

    private async Task SendFileTCPAsync()
    {
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
                Debug.Log($"[HeadTracker] Sending file to {targetIP}:{port}…");
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
        trackerData?.Close();
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "headtracking.csv");
            if (File.Exists(path)) File.Delete(path);
            trackerData = new StreamWriter(path, append: false);
            trackerData.AutoFlush = false;
            trackerData.WriteLine("timestamp_utc,rel_x,rel_y,rel_z");
            Debug.Log("[HeadTracker] File deleted.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HeadTracker] Failed to delete file: {ex.Message}");
        }
    }
}
