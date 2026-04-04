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
    private UdpClient      udpClient;
    private volatile IPEndPoint endPoint;   // volatile — updated from main thread, read by worker

    private TcpClient      tcpClient;
    private NetworkStream  networkStream;
    private readonly object _tcpLock = new object();

    private const int MaxUdpPacketSize = 1200;

    public string targetIP;   // shown in Inspector; overridden by NetworkDiscovery
    public int    targetPort;

    [SerializeField] private bool udpEnabled = false;
    [SerializeField] private bool no_split   = false;

    // Thread & queue
    private Thread workerThread;
    private ConcurrentQueue<byte[]> frameQueue = new ConcurrentQueue<byte[]>();
    private bool running = false;

    // Pending IP change (set from background event, applied on main thread)
    private volatile bool   _reconnectPending = false;
    private volatile string _pendingIP        = null;

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
        width  = WebCamTextureAccess.Instance.WebCamTexture.width;
        height = WebCamTextureAccess.Instance.WebCamTexture.height;
        Debug.Log($"[CurrentFrameCapturer] Webcam: {width}x{height}");

        running      = true;
        workerThread = new Thread(WorkerLoop) { IsBackground = true };
        workerThread.Start();
    }

    private void Update()
    {
        // Apply any IP change that arrived from the background thread
        if (_reconnectPending)
        {
            _reconnectPending = false;
            targetIP = _pendingIP;
            Debug.Log($"[CurrentFrameCapturer] Reconnecting to new Mac IP: {targetIP}");

            if (udpEnabled)
            {
                endPoint = new IPEndPoint(IPAddress.Parse(targetIP), targetPort);
            }
            else
            {
                ReconnectTCP();
            }
        }

        if (WebCamTextureAccess.Instance.WebCamTexture.isPlaying)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(WebCamTextureAccess.Instance.WebCamTexture.GetPixels32());
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

        lock (_tcpLock)
        {
            networkStream?.Close();
            tcpClient?.Close();
        }
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
        if (udpEnabled)
        {
            udpClient = new UdpClient();
            endPoint  = new IPEndPoint(IPAddress.Parse(targetIP), targetPort);
        }
        else
        {
            InitTCP();
        }
    }

    private void InitTCP()
    {
        lock (_tcpLock)
        {
            tcpClient = new TcpClient();
            try
            {
                tcpClient.Connect(targetIP, targetPort);
                networkStream = tcpClient.GetStream();
                Debug.Log($"[CurrentFrameCapturer] TCP connected to {targetIP}:{targetPort}");
            }
            catch (Exception e)
            {
                Debug.LogError("[CurrentFrameCapturer] TCP connection failed: " + e);
            }
        }
    }

    public void ReconnectTCP()
    {
        lock (_tcpLock)
        {
            try
            {
                networkStream?.Close();
                tcpClient?.Close();

                tcpClient = new TcpClient();
                tcpClient.Connect(targetIP, targetPort);
                networkStream = tcpClient.GetStream();
                Debug.Log($"[CurrentFrameCapturer] TCP reconnected to {targetIP}:{targetPort}");
            }
            catch (Exception e)
            {
                Debug.LogError("[CurrentFrameCapturer] TCP reconnection failed: " + e);
            }
        }
    }

    // ── Worker loop (background thread) ─────────────────────────────────── //

    private void WorkerLoop()
    {
        while (running)
        {
            if (frameQueue.TryDequeue(out var jpg))
            {
                if (udpEnabled)
                    SendFrameUDP(jpg);
                else
                    SendFrameTCP(jpg);
            }
            else
            {
                Thread.Sleep(5);
            }
        }
    }

    private void SendFrameUDP(byte[] jpg)
    {
        var ep = endPoint; // snapshot volatile ref
        if (ep == null) return;

        if (!no_split)
        {
            int totalPackets = (jpg.Length + MaxUdpPacketSize - 1) / MaxUdpPacketSize;
            for (int i = 0; i < totalPackets; i++)
            {
                int offset = i * MaxUdpPacketSize;
                int size   = Math.Min(MaxUdpPacketSize, jpg.Length - offset);

                byte[] packet = new byte[size + 2];
                packet[0] = (byte)i;
                packet[1] = (byte)totalPackets;
                Array.Copy(jpg, offset, packet, 2, size);

                udpClient.Send(packet, packet.Length, ep);
            }
        }
        else
        {
            udpClient.Send(jpg, jpg.Length, ep);
        }
    }

    private void SendFrameTCP(byte[] jpg)
    {
        lock (_tcpLock)
        {
            try
            {
                if (networkStream == null) return;
                byte[] lengthBytes = BitConverter.GetBytes(jpg.Length);
                networkStream.Write(lengthBytes, 0, lengthBytes.Length);
                networkStream.Write(jpg, 0, jpg.Length);
                networkStream.Flush();
            }
            catch (Exception)
            {
                // Silently drop — reconnect will be triggered by IP change or manual call
            }
        }
    }
}
