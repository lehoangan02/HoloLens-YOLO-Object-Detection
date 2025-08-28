using Assets.Scripts;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class CurrentFrameCapturer : MonoBehaviour
{
    private UdpClient udpClient;
    private IPEndPoint endPoint;

    private TcpClient tcpClient;
    private NetworkStream networkStream;

    private const int MaxUdpPacketSize = 1200;

    public Screamer screamer;
    public string targetIP;
    public int targetPort;

    [SerializeField]
    private bool udpEnabled = false;

    // Thread & queue
    private Thread workerThread;
    private ConcurrentQueue<byte[]> frameQueue = new ConcurrentQueue<byte[]>();
    private bool running = false;


    private int width, height;

    private void Start()
    {
        if (udpEnabled)
        {
            udpClient = new UdpClient();
            endPoint = new IPEndPoint(IPAddress.Parse(targetIP), targetPort);
        }
        else
        {
            initTCP();
        }

        WebCamTextureAccess.Instance.Play();
        width = WebCamTextureAccess.Instance.WebCamTexture.width;
        height = WebCamTextureAccess.Instance.WebCamTexture.height;

        Debug.Log($"Webcam image size: {width}x{height}");
        screamer.ScreamToDialog("Webcam image size: " + width + "x" + height);

        running = true;
        workerThread = new Thread(WorkerLoop);
        workerThread.IsBackground = true;
        workerThread.Start();
    }

    private void Update()
    {
        if (WebCamTextureAccess.Instance.WebCamTexture.isPlaying)
        {
            // Grab pixels (main thread)
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(WebCamTextureAccess.Instance.WebCamTexture.GetPixels32());
            tex.Apply();

            // Encode to JPG (still main thread, must be)
            byte[] jpg = tex.EncodeToJPG(50);
            Destroy(tex);

            // Queue JPG for background sending
            frameQueue.Enqueue(jpg);
        }
    }

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
        int totalPackets = (jpg.Length + MaxUdpPacketSize - 1) / MaxUdpPacketSize;
        for (int i = 0; i < totalPackets; i++)
        {
            int offset = i * MaxUdpPacketSize;
            int size = Math.Min(MaxUdpPacketSize, jpg.Length - offset);

            byte[] packet = new byte[size + 2];
            packet[0] = (byte)i;
            packet[1] = (byte)totalPackets;
            Array.Copy(jpg, offset, packet, 2, size);

            udpClient.Send(packet, packet.Length, endPoint);
        }
    }

    private void SendFrameTCP(byte[] jpg)
    {
        try
        {
            byte[] lengthBytes = BitConverter.GetBytes(jpg.Length);
            networkStream.Write(lengthBytes, 0, lengthBytes.Length);
            networkStream.Write(jpg, 0, jpg.Length);
            networkStream.Flush();
        }
        catch (Exception e)
        {
            Debug.Log("Send failed: " + e);
        }
    }

    private void initTCP()
    {
        tcpClient = new TcpClient();
        try
        {
            tcpClient.Connect(targetIP, targetPort);
            networkStream = tcpClient.GetStream();
            Debug.Log("TCP connected to " + targetIP + ":" + targetPort);
        }
        catch (Exception e)
        {
            Debug.LogError("TCP connection failed: " + e);
        }
    }

    private void OnDestroy()
    {
        running = false;
        workerThread?.Join();

        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
        if (tcpClient != null)
        {
            tcpClient.Close();
            tcpClient = null;
        }
        if (networkStream != null)
        {
            networkStream.Close();
            networkStream = null;
        }
    }
}
