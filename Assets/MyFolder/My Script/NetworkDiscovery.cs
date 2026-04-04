using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Singleton — bidirectional IP discovery between HoloLens and Mac.
///
/// Ports (must match Python networkmanager.py):
///   5010 — HoloLens broadcasts "HOLOLENS:{ip}" → Mac listens
///   5011 — Mac broadcasts "MAC:{ip}"            → HoloLens listens here
///
/// Timing:
///   0 – 60 s : broadcast every 2 s  (fast phase, to acquire IP quickly)
///   60 s +   : broadcast every 10 s (slow verify phase)
/// </summary>
public class NetworkDiscovery : MonoBehaviour
{
    public static NetworkDiscovery Instance { get; private set; }

    /// <summary>Mac IP discovered via broadcast. Null until first message received.</summary>
    public string MacIP { get; private set; }

    // ── Ports (mirror Python networkmanager.py) ─────────────────────────── //
    private const int HoloLensBroadcastPort = 5010;  // we send on this
    private const int MacListenPort          = 5011;  // we listen on this

    // ── Timing ──────────────────────────────────────────────────────────── //
    private const double FastDuration   = 60.0;  // seconds in fast phase
    private const int    FastIntervalMs = 2000;  // 2 s
    private const int    SlowIntervalMs = 10000; // 10 s

    private string          _holoLensIP;
    private Thread          _broadcastThread;
    private Thread          _listenThread;
    private volatile bool   _running;

    /// <summary>
    /// Raised from the background thread when the Mac IP is first found or changes.
    /// Subscribers that touch Unity objects must marshal to the main thread.
    /// </summary>
    public event Action<string> OnMacIPChanged;

    // ── Unity lifecycle ─────────────────────────────────────────────────── //

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _holoLensIP = GetLocalIP();
        Debug.Log($"[NetworkDiscovery] HoloLens IP: {_holoLensIP}");
    }

    private void Start()
    {
        _running = true;

        _broadcastThread = new Thread(BroadcastLoop) { IsBackground = true, Name = "ND-Broadcast" };
        _broadcastThread.Start();

        _listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "ND-Listen" };
        _listenThread.Start();
    }

    private void OnDestroy()
    {
        _running = false;
    }

    // ── Helpers ─────────────────────────────────────────────────────────── //

    private static string GetLocalIP()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError("[NetworkDiscovery] GetLocalIP: " + e.Message);
        }
        return "0.0.0.0";
    }

    // ── Broadcast thread — announces HoloLens IP to Mac ─────────────────── //

    private void BroadcastLoop()
    {
        var   startTime = DateTime.UtcNow;
        byte[] msg      = Encoding.UTF8.GetBytes($"HOLOLENS:{_holoLensIP}");
        var   ep        = new IPEndPoint(IPAddress.Broadcast, HoloLensBroadcastPort);

        using (var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        {
            sock.EnableBroadcast = true;
            while (_running)
            {
                try
                {
                    sock.SendTo(msg, ep);
                    Debug.Log($"[NetworkDiscovery] Broadcast HOLOLENS:{_holoLensIP}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[NetworkDiscovery] Broadcast error: {e.Message}");
                }

                double elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                int sleepMs = elapsed < FastDuration ? FastIntervalMs : SlowIntervalMs;
                Thread.Sleep(sleepMs);
            }
        }
    }

    // ── Listen thread — receives Mac IP broadcasts ───────────────────────── //

    private void ListenLoop()
    {
        try
        {
            using (var udp = new UdpClient())
            {
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, MacListenPort));
                udp.Client.ReceiveTimeout = 1000; // 1 s so we can check _running

                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                while (_running)
                {
                    try
                    {
                        byte[] data = udp.Receive(ref remoteEP);
                        string text = Encoding.UTF8.GetString(data).Trim();
                        if (text.StartsWith("MAC:"))
                        {
                            string ip = text.Substring(4);
                            if (ip != MacIP)
                            {
                                MacIP = ip;
                                Debug.Log($"[NetworkDiscovery] Mac IP: {ip}");
                                OnMacIPChanged?.Invoke(ip);
                            }
                        }
                    }
                    catch (SocketException) { }  // receive timeout — expected, loop again
                    catch (Exception e)
                    {
                        if (_running)
                            Debug.LogError($"[NetworkDiscovery] Listen error: {e.Message}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkDiscovery] Cannot open listen socket on port {MacListenPort}: {e.Message}");
        }
    }
}
