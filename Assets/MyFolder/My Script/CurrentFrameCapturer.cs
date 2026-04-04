using Assets.Scripts;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class CurrentFrameCapturer : MonoBehaviour
{
    private UdpClient           udpClient;
    private volatile IPEndPoint endPoint;  // volatile — updated from main thread, read by worker

    private const int  MaxUdpPacketSize = 8192;  // ~7 chunks/frame at 896×504 — stable on WiFi
    private const int  FramePort        = 5016;   // must match FRAME_PORT in controller_app.py
    private const bool no_split         = false;  // chunked UDP

    public string targetIP;   // shown in Inspector; overridden by NetworkDiscovery

    // UDP is the only supported mode — TCP frame streaming is not used
    private const bool udpEnabled = true;

    // Thread & queue
    private Thread workerThread;
    private ConcurrentQueue<byte[]> frameQueue = new ConcurrentQueue<byte[]>();
    private bool running = false;

    // Pending IP change (set from background event, applied on main thread)
    private volatile bool   _reconnectPending = false;
    private volatile string _pendingIP        = null;

    // Set to true only after Start() coroutine fully completes
    private bool   _initialized = false;
    private bool   _loggedFirst = false;
    private ushort _frameId     = 0;      // wraps 0–65535, used to detect frame boundaries

    public int width, height;

    // ── Unity lifecycle ──────────────────────────────────────────────────── //

    private IEnumerator Start()
    {
        // Wait until NetworkDiscovery has found the Mac IP
        if (NetworkDiscovery.Instance != null)
        {
            NetworkDiscovery.Instance.OnMacIPChanged += OnMacIPChanged;

            if (NetworkDiscovery.Instance.MacIP == null)
            {
                Debug.Log("[CurrentFrameCapturer] Waiting for Mac IP from NetworkDiscovery...");
                yield return new WaitUntil(() => NetworkDiscovery.Instance.MacIP != null);
            }
            targetIP = NetworkDiscovery.Instance.MacIP;
            Debug.Log($"[CurrentFrameCapturer] Using Mac IP: {targetIP}");
        }
        else
        {
            Debug.LogWarning("[CurrentFrameCapturer] NetworkDiscovery not found — using Inspector targetIP.");
        }

        InitNetwork();

        WebCamTextureAccess.Instance.Play();

        // Wait for the webcam to report a real resolution (default is 16x16 until ready)
        yield return new WaitUntil(() =>
            WebCamTextureAccess.Instance.WebCamTexture.width > 16);

        width  = WebCamTextureAccess.Instance.WebCamTexture.width;
        height = WebCamTextureAccess.Instance.WebCamTexture.height;
        Debug.Log($"[CurrentFrameCapturer] Webcam: {width}x{height}");

        running      = true;
        workerThread = new Thread(WorkerLoop) { IsBackground = true };
        workerThread.Start();

        _initialized = true;
    }

    private void Update()
    {
        // Apply any IP change that arrived from the background thread
        if (_reconnectPending)
        {
            _reconnectPending = false;
            targetIP = _pendingIP;
            endPoint = new IPEndPoint(IPAddress.Parse(targetIP), FramePort);
            Debug.Log($"[CurrentFrameCapturer] Updated UDP target: {targetIP}:{FramePort}");
        }

        if (!_initialized) return;

        var webcam = WebCamTextureAccess.Instance.WebCamTexture;
        if (webcam.isPlaying && webcam.width > 16)
        {
            // Use live dimensions — they may differ from the values captured at Start time
            var tex = new Texture2D(webcam.width, webcam.height, TextureFormat.RGBA32, false);
            tex.SetPixels32(webcam.GetPixels32());
            tex.Apply();

            byte[] jpg = tex.EncodeToJPG(50);
            Destroy(tex);

            frameQueue.Enqueue(jpg);
        }
    }

    private void OnDestroy()
    {
        running = false;
        workerThread?.Join();

        if (NetworkDiscovery.Instance != null)
            NetworkDiscovery.Instance.OnMacIPChanged -= OnMacIPChanged;

        udpClient?.Close();
    }

    // ── NetworkDiscovery callback (background thread) ────────────────────── //

    private void OnMacIPChanged(string ip)
    {
        _pendingIP        = ip;
        _reconnectPending = true;  // handled safely in Update()
    }

    // ── Network init ─────────────────────────────────────────────────────── //

    private void InitNetwork()
    {
        udpClient = new UdpClient();
        endPoint  = new IPEndPoint(IPAddress.Parse(targetIP), FramePort);
        Debug.Log($"[CurrentFrameCapturer] UDP target: {targetIP}:{FramePort}");
    }

    // ── Worker loop (background thread) ─────────────────────────────────── //

    private void WorkerLoop()
    {
        while (running)
        {
            if (frameQueue.TryDequeue(out var jpg))
                SendFrameUDP(jpg);
            else
                Thread.Sleep(5);
        }
    }

    private void SendFrameUDP(byte[] jpg)
    {
        var ep = endPoint; // snapshot volatile ref
        if (ep == null) return;

        if (!_loggedFirst)
        {
            var wc = WebCamTextureAccess.Instance.WebCamTexture;
            int chunks = (jpg.Length + MaxUdpPacketSize - 1) / MaxUdpPacketSize;
            Debug.Log($"[CurrentFrameCapturer] First frame: {wc.width}x{wc.height}  JPEG={jpg.Length/1024}KB  chunks={chunks}  →{ep}");
            _loggedFirst = true;
        }

        // Packet header (4 bytes):
        //   [0–1] frame_id  (big-endian ushort — resets on new frame)
        //   [2]   chunk_index
        //   [3]   total_chunks
        ushort fid = _frameId++;
        int totalPackets = (jpg.Length + MaxUdpPacketSize - 1) / MaxUdpPacketSize;

        for (int i = 0; i < totalPackets; i++)
        {
            int offset = i * MaxUdpPacketSize;
            int size   = Math.Min(MaxUdpPacketSize, jpg.Length - offset);

            byte[] packet = new byte[size + 4];
            packet[0] = (byte)(fid >> 8);
            packet[1] = (byte)(fid & 0xFF);
            packet[2] = (byte)i;
            packet[3] = (byte)totalPackets;
            Array.Copy(jpg, offset, packet, 4, size);

            udpClient.Send(packet, packet.Length, ep);
        }
    }

}
