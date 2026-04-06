using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Listens for UDP commands from the Mac controller app and dispatches them
/// to EyeTracker / HeadTracker on the Unity main thread.
///
/// Supported commands (port 5015):
///   CMD:START_EYE   CMD:STOP_EYE   CMD:SEND_EYE
///   CMD:START_HEAD  CMD:STOP_HEAD  CMD:SEND_HEAD
///   CMD:START_LABEL CMD:STOP_LABEL CMD:SEND_LABEL
///   CMD:SEND_MARKERS  CMD:RESET
///
/// Attach to any active GameObject. Assign eyeTracker and headTracker
/// in the Inspector.
/// </summary>
public class CommandReceiver : MonoBehaviour
{
    public EyeTracker  eyeTracker;
    public HeadTracker headTracker;
    public LabelTracker labelTracker;

    private const int CmdPort = 5015;
    private const int MarkerFilePort = 5017;

    private UdpClient udpClient;
    private Thread    receiveThread;
    private bool      running;
    private StreamWriter markerWriter;
    private string markerPath;

    private readonly ConcurrentQueue<ReceivedPacket> _cmdQueue =
        new ConcurrentQueue<ReceivedPacket>();

    [Serializable]
    private class SyncMarkerMessage
    {
        public string type;
        public int version;
        public string marker_id;
        public string label;
        public string participant;
        public string source;
        public string sent_utc;
        public long sent_unix_ms;
        public string notes;
    }

    private struct ReceivedPacket
    {
        public string Message;
        public string RemoteAddress;

        public ReceivedPacket(string message, string remoteAddress)
        {
            Message = message;
            RemoteAddress = remoteAddress;
        }
    }

    // ── Unity lifecycle ──────────────────────────────────────────────── //

    private void Start()
    {
        // Auto-find trackers if not assigned in Inspector
        if (eyeTracker  == null) eyeTracker  = FindObjectOfType<EyeTracker>();
        if (headTracker == null) headTracker = FindObjectOfType<HeadTracker>();
        if (labelTracker == null) labelTracker = FindObjectOfType<LabelTracker>();
        if (labelTracker == null)
            labelTracker = gameObject.AddComponent<LabelTracker>();
        if (labelTracker != null && labelTracker.hitPointDisplayPrefab == null &&
            eyeTracker != null && eyeTracker.hitPointDisplayPrefab != null)
        {
            labelTracker.hitPointDisplayPrefab = eyeTracker.hitPointDisplayPrefab;
        }
        if (labelTracker != null && eyeTracker != null)
            labelTracker.maxGazeDistance = Mathf.Max(labelTracker.maxGazeDistance, eyeTracker.maxGazeDistance);
        if (labelTracker != null && eyeTracker != null && eyeTracker.gazeInteractor != null)
            labelTracker.SetGazeInteractor(eyeTracker.gazeInteractor);

        if (eyeTracker  == null) Debug.LogWarning("[CommandReceiver] EyeTracker not found");
        if (headTracker == null) Debug.LogWarning("[CommandReceiver] HeadTracker not found");
        if (labelTracker == null) Debug.LogWarning("[CommandReceiver] LabelTracker not found");

        markerPath = Path.Combine(Application.persistentDataPath, "sync_markers.csv");
        OpenMarkerWriter(append: true);

        udpClient = new UdpClient(CmdPort);
        udpClient.Client.ReceiveTimeout = 1000; // so the thread can check `running`
        running = true;

        receiveThread = new Thread(ReceiveLoop)
            { IsBackground = true, Name = "CmdReceiver" };
        receiveThread.Start();

        Debug.Log($"[CommandReceiver] Listening on UDP port {CmdPort}");
    }

    private void Update()
    {
        // Drain command queue on the main thread (Unity API is single-threaded)
        while (_cmdQueue.TryDequeue(out ReceivedPacket packet))
            Dispatch(packet);
    }

    private void OnDestroy()
    {
        running = false;
        udpClient?.Close();
        markerWriter?.Close();
    }

    // ── Background receive thread ────────────────────────────────────── //

    private void ReceiveLoop()
    {
        var ep = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref ep);
                string cmd  = Encoding.UTF8.GetString(data).Trim();
                Debug.Log($"[CommandReceiver] ← {cmd}  from {ep}");
                _cmdQueue.Enqueue(new ReceivedPacket(cmd, ep.Address.ToString()));
            }
            catch (SocketException) { }  // ReceiveTimeout — expected, loop again
            catch (Exception e)
            {
                if (running)
                    Debug.LogError($"[CommandReceiver] Error: {e.Message}");
            }
        }
    }

    // ── Command dispatch (main thread) ───────────────────────────────── //

    private void Dispatch(ReceivedPacket packet)
    {
        string cmd = packet.Message;

        if (TryLogSyncMarker(cmd, packet.RemoteAddress))
            return;

        switch (cmd)
        {
            case "CMD:START_EYE":   eyeTracker?.TurnOn();     break;
            case "CMD:STOP_EYE":    eyeTracker?.TurnOff();    break;
            case "CMD:SEND_EYE":    eyeTracker?.SendFile();   break;
            case "CMD:START_HEAD":  headTracker?.TurnOn();    break;
            case "CMD:STOP_HEAD":   headTracker?.TurnOff();   break;
            case "CMD:SEND_HEAD":   headTracker?.SendFile();  break;
            case "CMD:START_LABEL": labelTracker?.TurnOn();   break;
            case "CMD:STOP_LABEL":  labelTracker?.TurnOff();  break;
            case "CMD:SEND_LABEL":  labelTracker?.SendFile(); break;
            case "CMD:SEND_MARKERS": SendMarkerFile();        break;
            case "CMD:RESET":
                eyeTracker?.TurnOff();
                headTracker?.TurnOff();
                labelTracker?.TurnOff();
                eyeTracker?.DeleteFile();
                headTracker?.DeleteFile();
                labelTracker?.DeleteFile();
                ResetMarkerFile();
                break;
            default:
                Debug.LogWarning($"[CommandReceiver] Unknown command: {cmd}");
                break;
        }
    }

    private bool TryLogSyncMarker(string rawJson, string remoteAddress)
    {
        if (string.IsNullOrWhiteSpace(rawJson) || rawJson[0] != '{')
            return false;

        SyncMarkerMessage marker;
        try
        {
            marker = JsonUtility.FromJson<SyncMarkerMessage>(rawJson);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CommandReceiver] Could not parse marker JSON: {e.Message}");
            return false;
        }

        if (marker == null || marker.type != "sync_marker")
            return false;

        try
        {
            string receivedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            long receivedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            markerWriter?.WriteLine(string.Join(",",
                Csv(receivedUtc),
                receivedUnixMs.ToString(),
                Csv(marker.marker_id),
                Csv(marker.label),
                Csv(marker.participant),
                Csv(marker.source),
                Csv(marker.sent_utc),
                marker.sent_unix_ms.ToString(),
                Csv(remoteAddress),
                Csv(marker.notes),
                Csv(rawJson)
            ));
            markerWriter?.Flush();
            Debug.Log($"[CommandReceiver] Logged sync marker {marker.label}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CommandReceiver] Failed to log marker: {e.Message}");
        }
        return true;
    }

    private void OpenMarkerWriter(bool append)
    {
        bool writeHeader = !append || !File.Exists(markerPath) || new FileInfo(markerPath).Length == 0;
        markerWriter = new StreamWriter(markerPath, append, Encoding.UTF8);
        markerWriter.AutoFlush = true;
        if (writeHeader)
        {
            markerWriter.WriteLine(
                "received_utc,received_unix_ms,marker_id,label,participant,source," +
                "sent_utc,sent_unix_ms,sender_ip,notes,payload_json"
            );
        }
    }

    private void ResetMarkerFile()
    {
        try
        {
            markerWriter?.Close();
            if (File.Exists(markerPath))
                File.Delete(markerPath);
            OpenMarkerWriter(append: false);
            Debug.Log("[CommandReceiver] Marker file reset");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CommandReceiver] Failed to reset marker file: {e.Message}");
        }
    }

    private async void SendMarkerFile()
    {
        markerWriter?.Flush();
        markerWriter?.Close();
        markerWriter = null;

        try
        {
            await SendFileTCPAsync(MarkerFilePort, markerPath, "sync marker file");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CommandReceiver] Failed to send marker file: {ex.Message}");
        }
        finally
        {
            OpenMarkerWriter(append: true);
        }
    }

    private async Task SendFileTCPAsync(int port, string filePath, string label)
    {
        string targetIP = NetworkDiscovery.Instance?.MacIP;
        if (string.IsNullOrEmpty(targetIP))
        {
            Debug.LogWarning("[CommandReceiver] MacIP not yet known — waiting up to 5 s…");
            for (int i = 0; i < 50 && string.IsNullOrEmpty(targetIP); i++)
            {
                await Task.Delay(100);
                targetIP = NetworkDiscovery.Instance?.MacIP;
            }
        }
        if (string.IsNullOrEmpty(targetIP))
        {
            Debug.LogError("[CommandReceiver] Mac IP not discovered after 5 s — cannot send file.");
            return;
        }

        using (TcpClient client = new TcpClient())
        {
            await client.ConnectAsync(targetIP, port);
            using (NetworkStream stream = client.GetStream())
            using (FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Read))
            {
                Debug.Log($"[CommandReceiver] Sending {label} to {targetIP}:{port}…");
                byte[] buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    await stream.WriteAsync(buffer, 0, bytesRead);
                Debug.Log($"[CommandReceiver] Sent {label}.");
            }
        }
    }

    private static string Csv(string value)
    {
        string safe = value ?? string.Empty;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
