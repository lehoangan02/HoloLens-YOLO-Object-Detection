using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UDPClient : MonoBehaviour
{
    UdpClient udpClient;
    Thread receiveThread;

    public string pythonIP;  // Python machine IP
    public int pythonPort;
    public int unityPort;

    public Screamer screamer;

    void Start()
    {
        pythonIP = "10.0.10.203";
        pythonPort = 5007;
        unityPort = 5008;
        Debug.Log($"Starting UDP Client - Unity Port: {unityPort}, Python IP: {pythonIP}, Python Port: {pythonPort}");
        udpClient = new UdpClient(unityPort);
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log("UDP Client started and listening for messages...");
    }
    string LogIPAddress()
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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Space key pressed! Sending message to Python...");
            SendToPython(LogIPAddress());
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            Debug.Log("Printing IP\n");
            LogIPAddress();

        }
    }

    void SendToPython(string message)
    {
        try
        {
            Debug.Log($"Attempting to send to Python at {pythonIP}:{pythonPort}");
            UdpClient sender = new UdpClient(); // Let system assign a random port
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
        if (udpClient != null) udpClient.Close();
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
