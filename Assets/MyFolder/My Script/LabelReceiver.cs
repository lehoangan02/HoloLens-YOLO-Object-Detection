using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using Assets.Scripts;
using System;

public class LabelReceiver : MonoBehaviour
{
    [SerializeField]
    private bool udpEnabled = true;

    private UdpClient udpClient;
    private TcpClient tcpClient;
    private NetworkStream networkStream;
    private Thread receiveThread;
    private volatile bool running;
    private const int Port = 5014;

    private readonly List<YoloItem> latestDetectionsYolo = new();
    private YoloRecognitionHandler yoloRecognitionHandler;
    private CameraTransform cameraTransform;

    void Start()
    {
        running = true;
        if (udpEnabled)
            InitUDP();
        else
            InitTCP();

        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log($"Listening for YOLO detections on port {Port}...");

        yoloRecognitionHandler = gameObject.GetComponent<YoloRecognitionHandler>();
        if (yoloRecognitionHandler == null)
            Debug.LogError("YoloRecognitionHandler component not found on the GameObject.");
    }

    private void InitUDP()
    {
        udpClient = new UdpClient(Port);
        receiveThread = new Thread(ReceiveDataUDP);
    }

    private void InitTCP()
    {
        try
        {
            tcpClient = new TcpClient("192.168.1.8", Port);
            networkStream = tcpClient.GetStream();
            receiveThread = new Thread(ReceiveDataTCP);
            Debug.Log("TCP connected to Python server.");
        }
        catch (Exception e)
        {
            Debug.LogError("TCP connection failed: " + e.Message);
        }
    }

    private void ReceiveDataTCP()
    {
        try
        {
            while (running)
            {
                byte[] lengthBytes = new byte[4];
                int read = networkStream.Read(lengthBytes, 0, 4);
                if (read < 4)
                {
                    Debug.LogWarning("TCP connection closed by server.");
                    break;
                }
                int msgLength = BitConverter.ToInt32(lengthBytes, 0);

                byte[] msgBytes = new byte[msgLength];
                int totalRead = 0;
                while (totalRead < msgLength)
                {
                    int r = networkStream.Read(msgBytes, totalRead, msgLength - totalRead);
                    if (r == 0)
                    {
                        Debug.LogWarning("TCP connection closed while receiving data.");
                        return;
                    }
                    totalRead += r;
                }

                string json = Encoding.UTF8.GetString(msgBytes);
                Detection[] detections = JsonHelper.FromJson<Detection>(json);
                if (detections == null) continue;

                lock (latestDetectionsYolo)
                {
                    latestDetectionsYolo.Clear();
                    foreach (var det in detections)
                        latestDetectionsYolo.Add(DetectionToYoloItem(det));
                }
            }
        }
        catch (Exception e)
        {
            if (running) Debug.LogError("TCP Receive error: " + e.Message);
        }
    }

    private void ReceiveDataUDP()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEP);
                string json = Encoding.UTF8.GetString(data);
                Detection[] detections = JsonHelper.FromJson<Detection>(json);
                if (detections == null) continue;

                lock (latestDetectionsYolo)
                {
                    latestDetectionsYolo.Clear();
                    foreach (var det in detections)
                        latestDetectionsYolo.Add(DetectionToYoloItem(det));
                }
            }
            catch (Exception)
            {
                // socket closed or malformed packet — loop will exit via running flag
            }
        }
    }

    void Update()
    {
        if (yoloRecognitionHandler == null) return;

        cameraTransform = new CameraTransform(Camera.main);

        // Snapshot inside lock, then release before heavy work
        List<YoloItem> snapshot;
        lock (latestDetectionsYolo)
        {
            if (latestDetectionsYolo.Count == 0) return;
            snapshot = new List<YoloItem>(latestDetectionsYolo);
        }

        yoloRecognitionHandler.ShowRecognitions(snapshot, cameraTransform);
    }

    private void OnDestroy()
    {
        running = false;
        udpClient?.Close();
        networkStream?.Close();
        tcpClient?.Close();
        receiveThread?.Join(500);
    }

    private static YoloItem DetectionToYoloItem(Detection det)
    {
        return YoloItem.FromVersion8Food(
            new Vector2(det.bbox.cx, det.bbox.cy),
            new Vector2(det.bbox.w, det.bbox.h),
            det.confidence,
            det.@class
        );
    }

    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            string newJson = "{\"array\":" + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper?.array;
        }

        [System.Serializable]
        private class Wrapper<T>
        {
            public T[] array;
        }
    }

    [System.Serializable]
    public class Detection
    {
        public string @class;
        public BBox bbox;
        public float confidence;

        [System.Serializable]
        public class BBox
        {
            public float cx, cy, w, h;
        }
    }
}
