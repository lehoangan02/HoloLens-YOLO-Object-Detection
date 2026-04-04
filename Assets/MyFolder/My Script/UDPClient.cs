using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UDPClient : MonoBehaviour
{
    UdpClient udpClient;
    Thread receiveThread;

    public string pythonIP;   // populated by NetworkDiscovery
    public int pythonPort;
    public int unityPort;

    public Screamer screamer;

    void Start()
    {
        pythonPort = 5007;
        unityPort  = 5008;

        udpClient = new UdpClient(unityPort);
        receiveThread = new Thread(new ThreadStart(ReceiveData)) { IsBackground = true };
        receiveThread.Start();
        Debug.Log($"[UDPClient] Listening on port {unityPort}, waiting for Mac IP...");

        // Subscribe to discovery; apply immediately if already known
        if (NetworkDiscovery.Instance != null)
        {
            NetworkDiscovery.Instance.OnMacIPChanged += OnMacIPChanged;
            if (NetworkDiscovery.Instance.MacIP != null)
                OnMacIPChanged(NetworkDiscovery.Instance.MacIP);
        }
        else
        {
            Debug.LogWarning("[UDPClient] NetworkDiscovery not found — pythonIP must be set manually.");
        }
    }

    private void OnMacIPChanged(string ip)
    {
        pythonIP = ip;
        Debug.Log($"[UDPClient] Mac IP updated: {pythonIP}");
    }

    public string LogIPAddress()
    {
        string localIP = string.Empty;
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                    break;
                }
            }
            Debug.Log("Local IP Address: " + localIP);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error retrieving local IP address: " + e.ToString());
        }
        return localIP;
    }

    void ReceiveData()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            try
            {
                byte[] data = udpClient.Receive(ref anyIP);
                string text = Encoding.UTF8.GetString(data);
                Debug.Log("Received from Python: " + text);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Receive error: " + e.ToString());
            }
        }
    }

    void Update() { }

    void SendToPython(string message)
    {
        if (string.IsNullOrEmpty(pythonIP))
        {
            Debug.LogWarning("[UDPClient] Mac IP not yet known — cannot send.");
            return;
        }
        try
        {
            Debug.Log($"[UDPClient] Sending to {pythonIP}:{pythonPort}");
            UdpClient sender = new UdpClient();
            byte[] data = Encoding.UTF8.GetBytes(message);
            sender.Send(data, data.Length, pythonIP, pythonPort);
            sender.Close();
            Debug.Log("Message sent to Python: " + message);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Send error: " + e.ToString());
        }
    }

    private void OnApplicationQuit()
    {
        if (receiveThread != null) receiveThread.Abort();
        if (udpClient    != null) udpClient.Close();
        if (NetworkDiscovery.Instance != null)
            NetworkDiscovery.Instance.OnMacIPChanged -= OnMacIPChanged;
    }

    private int i = 0;
    public void SendToPythonAndScreamMessage(string message)
    {
        ++i;
        string toPythonMessage = i + " time: Hello python " + message;
        screamer.ScreamToDialog(toPythonMessage);
        SendToPython(toPythonMessage);
    }
}
