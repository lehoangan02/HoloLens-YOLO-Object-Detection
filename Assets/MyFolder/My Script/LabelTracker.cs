using MixedReality.Toolkit.Input;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

public class LabelTracker : MonoBehaviour
{
    public GazeInteractor gazeInteractor;
    public GameObject hitPointDisplayPrefab;
    public float maxGazeDistance = 5.0f;
    public int port = 5018;

    private GameObject hitPointDisplayer;
    private StreamWriter trackerData;
    private bool isTrackingEnabled = false;
    private int _writeCount = 0;

    private void Awake()
    {
        string path = Path.Combine(Application.persistentDataPath, "labeltracking.csv");
        trackerData = new StreamWriter(path);
        trackerData.AutoFlush = false;
        trackerData.WriteLine(
            "timestamp_utc,label_name,world_x,world_y,world_z,local_x,local_y,local_z,hit_distance_m"
        );
    }

    private void Start()
    {
        if (gazeInteractor == null)
            gazeInteractor = FindObjectOfType<GazeInteractor>();

        if (hitPointDisplayPrefab != null)
        {
            hitPointDisplayer = Instantiate(hitPointDisplayPrefab);
            hitPointDisplayer.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isTrackingEnabled)
            return;

        if (gazeInteractor == null)
            gazeInteractor = FindObjectOfType<GazeInteractor>();
        if (gazeInteractor == null)
            return;

        Ray ray = new Ray(
            gazeInteractor.rayOriginTransform.position,
            gazeInteractor.rayOriginTransform.forward
        );

        if (!TryFindLabelHit(ray, out RaycastHit hit, out NutritionLabelController label))
            return;

        hitPointDisplayer?.SetActive(true);
        if (hitPointDisplayer != null)
            hitPointDisplayer.transform.position = hit.point;

        WriteTrackingPoint(label, hit);
    }

    private bool TryFindLabelHit(
        Ray ray,
        out RaycastHit labelHit,
        out NutritionLabelController labelController
    )
    {
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            maxGazeDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            NutritionLabelController label = hit.collider.GetComponentInParent<NutritionLabelController>();
            if (label == null)
                continue;

            labelHit = hit;
            labelController = label;
            return true;
        }

        labelHit = default;
        labelController = null;
        return false;
    }

    private void WriteTrackingPoint(NutritionLabelController label, RaycastHit hit)
    {
        if (trackerData == null)
            return;

        string ts = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        Vector3 localPoint = label.transform.InverseTransformPoint(hit.point);

        trackerData.WriteLine(FormattableString.Invariant(
            $"{ts},{EscapeCsv(label.CurrentFoodName)},{hit.point.x},{hit.point.y},{hit.point.z}," +
            $"{localPoint.x},{localPoint.y},{localPoint.z},{hit.distance}"
        ));
        if (++_writeCount % 30 == 0)
            trackerData.Flush();
    }

    private static string EscapeCsv(string value)
    {
        string safe = value ?? string.Empty;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }

    private void OnDestroy()
    {
        trackerData?.Close();
    }

    public void SetGazeInteractor(GazeInteractor source)
    {
        gazeInteractor = source;
    }

    public void TurnOn()
    {
        if (isTrackingEnabled)
            return;

        isTrackingEnabled = true;
        hitPointDisplayer?.SetActive(true);
        Debug.Log("[LabelTracker] Tracking ON");
    }

    public void TurnOff()
    {
        if (!isTrackingEnabled)
            return;

        isTrackingEnabled = false;
        hitPointDisplayer?.SetActive(false);
        Debug.Log("[LabelTracker] Tracking OFF");
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
            Debug.LogError($"[LabelTracker] Failed to send file: {ex.Message}");
        }
        finally
        {
            string path = Path.Combine(Application.persistentDataPath, "labeltracking.csv");
            trackerData = new StreamWriter(path, append: true);
            trackerData.AutoFlush = false;
        }
    }

    private async Task SendFileTCPAsync()
    {
        string targetIP = NetworkDiscovery.Instance?.MacIP;
        if (string.IsNullOrEmpty(targetIP))
        {
            Debug.LogWarning("[LabelTracker] MacIP not yet known — waiting up to 5 s…");
            for (int i = 0; i < 50 && string.IsNullOrEmpty(targetIP); i++)
            {
                await Task.Delay(100);
                targetIP = NetworkDiscovery.Instance?.MacIP;
            }
        }
        if (string.IsNullOrEmpty(targetIP))
        {
            Debug.LogError("[LabelTracker] Mac IP not discovered after 5 s — cannot send file.");
            return;
        }

        string filePath = Path.Combine(Application.persistentDataPath, "labeltracking.csv");
        using (TcpClient client = new TcpClient())
        {
            await client.ConnectAsync(targetIP, port);
            using (NetworkStream stream = client.GetStream())
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                Debug.Log($"[LabelTracker] Sending file to {targetIP}:{port}…");
                byte[] buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    await stream.WriteAsync(buffer, 0, bytesRead);
                Debug.Log("[LabelTracker] File sent.");
            }
        }
    }

    public void DeleteFile()
    {
        trackerData?.Close();
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "labeltracking.csv");
            if (File.Exists(path))
                File.Delete(path);
            trackerData = new StreamWriter(path, append: false);
            trackerData.AutoFlush = false;
            trackerData.WriteLine(
                "timestamp_utc,label_name,world_x,world_y,world_z,local_x,local_y,local_z,hit_distance_m"
            );
            Debug.Log("[LabelTracker] Deleted labeltracking.csv");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LabelTracker] Failed to delete file: {ex.Message}");
        }
    }
}
