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

    private const int  MaxUdpPacketSize = 1400;  // below WiFi MTU (1472) — no IP fragmentation
    private const int  FramePort        = 5016;   // must match FRAME_PORT in controller_app.py
    private const bool no_split         = false;  // chunked UDP
    private const int  SendWidth        = 640;    // output resolution (16:9, matches camera AR)
    private const int  SendHeight       = 360;

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
    private ushort _frameId     = 0;

    public int width, height;

    // Pre-allocated GPU/CPU resources — avoids per-frame GC allocation
    private RenderTexture _sendRT;
    private Texture2D     _sendTex;
    private readonly Rect _sendRect = new Rect(0, 0, SendWidth, SendHeight);

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

        _sendRT  = new RenderTexture(SendWidth, SendHeight, 0, RenderTextureFormat.ARGB32);
        _sendTex = new Texture2D(SendWidth, SendHeight, TextureFormat.RGBA32, false);

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
        // didUpdateThisFrame: skip frames where the camera hasn't produced new data
        // (Update runs at ~60fps but camera is only 4fps — avoids 15 redundant encodes)
        if (webcam.isPlaying && webcam.didUpdateThisFrame)
        {
            Graphics.Blit(webcam, _sendRT);

            var prev = RenderTexture.active;
            RenderTexture.active = _sendRT;
            _sendTex.ReadPixels(_sendRect, 0, 0);
            _sendTex.Apply();
            RenderTexture.active = prev;

            byte[] jpg = _sendTex.EncodeToJPG(50);

            // Drop oldest frame if worker hasn't caught up — prefer latest frame
            while (frameQueue.Count > 1)
                frameQueue.TryDequeue(out _);

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
        _sendRT?.Release();
        if (_sendTex != null) Destroy(_sendTex);
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
            int chunks = (jpg.Length + MaxUdpPacketSize - 1) / MaxUdpPacketSize;
            Debug.Log($"[CurrentFrameCapturer] First frame: {SendWidth}x{SendHeight} (camera {width}x{height})  JPEG={jpg.Length/1024}KB  chunks={chunks}  →{ep}");
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
