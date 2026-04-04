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

    /// <summary>
    /// Uses the "connect to 8.8.8.8" trick to return the IP of the interface
    /// that has a default route — reliably picks WiFi over USB/loopback.
    /// </summary>
    private static string GetLocalIP()
    {
        try
        {
            using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                s.Connect("8.8.8.8", 80);
                return ((IPEndPoint)s.LocalEndPoint).Address.ToString();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[NetworkDiscovery] GetLocalIP UDP trick failed: " + e.Message);
        }
        // Fallback: DNS enumeration
        try
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
        }
        catch { }
        return "0.0.0.0";
    }

    /// <summary>
    /// Returns the /24 subnet-directed broadcast for the given IP.
    /// e.g. "10.0.10.7" → "10.0.10.255"
    /// </summary>
    private static IPAddress SubnetBroadcast(string localIP)
    {
        try
        {
            var parts = localIP.Split('.');
            if (parts.Length == 4)
                return IPAddress.Parse($"{parts[0]}.{parts[1]}.{parts[2]}.255");
        }
        catch { }
        return IPAddress.Broadcast;
    }

    // ── Broadcast thread — announces HoloLens IP to Mac ─────────────────── //

    private void BroadcastLoop()
    {
        var   startTime  = DateTime.UtcNow;
        byte[] msg       = Encoding.UTF8.GetBytes($"HOLOLENS:{_holoLensIP}");
        var   subnetEP   = new IPEndPoint(SubnetBroadcast(_holoLensIP), HoloLensBroadcastPort);
        var   broadcastEP = new IPEndPoint(IPAddress.Broadcast, HoloLensBroadcastPort);

        using (var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        {
            sock.EnableBroadcast = true;
            while (_running)
            {
                try
                {
                    // Send to both 255.255.255.255 and subnet-directed broadcast
                    // for maximum compatibility across routers
                    sock.SendTo(msg, subnetEP);
                    sock.SendTo(msg, broadcastEP);
                    //Debug.Log($"[NetworkDiscovery] Broadcast HOLOLENS:{_holoLensIP} → {subnetEP.Address} + 255.255.255.255");
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
        Debug.Log("[NetworkDiscovery] ListenLoop thread started");
        try
        {
            // Set ReuseAddress BEFORE binding (required on Windows)
            using (var udp = new UdpClient())
            {
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, MacListenPort));
                udp.Client.ReceiveTimeout = 1000;

                //Debug.Log($"[NetworkDiscovery] Socket bound — listening on port {MacListenPort}");

                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                while (_running)
                {
                    try
                    {
                        byte[] data = udp.Receive(ref remoteEP);
                        //Debug.Log($"[NetworkDiscovery] Packet from {remoteEP}: {data.Length} bytes");
                        string text = Encoding.UTF8.GetString(data).Trim();
                        //Debug.Log($"[NetworkDiscovery] Message: '{text}'");
                        if (text.StartsWith("MAC:"))
                        {
                            string ip = text.Substring(4);
                            if (ip != MacIP)
                            {
                                MacIP = ip;
                                //Debug.Log($"[NetworkDiscovery] Mac IP: {ip}");
                                OnMacIPChanged?.Invoke(ip);
                            }
                        }
                    }
                    catch (SocketException se)
                    {
                        // TimedOut (10060) is expected every 1 s — log anything else
                        if (se.SocketErrorCode != SocketError.TimedOut)
                            Debug.LogWarning($"[NetworkDiscovery] SocketException: {se.SocketErrorCode} — {se.Message}");
                    }
                    catch (Exception e)
                    {
                        if (_running)
                            Debug.LogError($"[NetworkDiscovery] Listen error: {e.GetType().Name} — {e.Message}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkDiscovery] Cannot bind port {MacListenPort}: {e.GetType().Name} — {e.Message}");
        }
        Debug.Log("[NetworkDiscovery] ListenLoop thread ended");
    }
}
