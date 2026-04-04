using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    // Port HoloLens broadcasts its IP on (Mac listens here)
    private const int HOLOLENS_BROADCAST_PORT = 5010;
    // Port Mac broadcasts its IP on (HoloLens listens here)
    private const int MAC_BROADCAST_PORT = 5011;

    private const float BROADCAST_INTERVAL = 2f;

    private string _localIP = "";
    private string _macIP = "";
    private UdpClient _listener;
    private Thread _listenThread;
    private bool _running = false;

    public string MacIP => _macIP;
    public string LocalIP => _localIP;

    void Start()
    {
        _localIP = GetLocalIPAddress();
        Debug.Log($"[NetworkManager] HoloLens local IP: {_localIP}");

        _running = true;
        _listenThread = new Thread(ListenForMacIP) { IsBackground = true };
        _listenThread.Start();

        StartCoroutine(BroadcastLoop());
    }

    void OnDestroy()
    {
        _running = false;
        _listener?.Close();
        _listenThread?.Abort();
    }

    // Continuously broadcast HoloLens IP so the Mac can discover it
    private IEnumerator BroadcastLoop()
    {
        while (_running)
        {
            BroadcastLocalIP();
            yield return new WaitForSeconds(BROADCAST_INTERVAL);
        }
    }

    private void BroadcastLocalIP()
    {
        try
        {
            using var client = new UdpClient();
            client.EnableBroadcast = true;
            byte[] data = Encoding.UTF8.GetBytes($"HOLOLENS:{_localIP}");
            client.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, HOLOLENS_BROADCAST_PORT));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[NetworkManager] Broadcast error: {e.Message}");
        }
    }

    // Listen on port 5011 for the Mac's broadcast
    private void ListenForMacIP()
    {
        try
        {
            _listener = new UdpClient(MAC_BROADCAST_PORT);
            _listener.EnableBroadcast = true;
            var endpoint = new IPEndPoint(IPAddress.Any, MAC_BROADCAST_PORT);

            while (_running)
            {
                byte[] data = _listener.Receive(ref endpoint);
                string message = Encoding.UTF8.GetString(data);

                if (message.StartsWith("MAC:"))
                {
                    string ip = message.Substring(4).Trim();
                    if (_macIP != ip)
                    {
                        _macIP = ip;
                        Debug.Log($"[NetworkManager] Mac IP received: {_macIP}");
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            if (_running)
                Debug.LogWarning($"[NetworkManager] Listen error: {e.Message}");
        }
    }

    private string GetLocalIPAddress()
    {
        try
        {
            foreach (var addr in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                    return addr.ToString();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[NetworkManager] Could not get local IP: {e.Message}");
        }
        return "0.0.0.0";
    }
}
