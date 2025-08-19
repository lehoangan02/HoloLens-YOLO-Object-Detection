using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class LabelReceiver : MonoBehaviour
{
    UdpClient udpClient;
    Thread receiveThread;
    int port = 5011;
    [System.Serializable]
    public class Detection
    {
        public string @class;
        public int centerX;
        public int centerY;
        public float confidence;
    }
    List<Detection> latestDetections = new List<Detection>();
    void Start()
    {
        udpClient = new UdpClient(port);
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log($"Listening for YOLO detections on port {port}...");
    }
    void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEP);
                string json = Encoding.UTF8.GetString(data);
                Detection[] detections = JsonHelper.FromJson<Detection>(json);

                lock (latestDetections)
                {
                    latestDetections.Clear();
                    latestDetections.AddRange(detections);
                }
            }
            catch (System.Exception e)
            {
                Debug.Log("UDP Receive error: " + e.Message);
            }
        }
    }
    // Update is called once per frame
    void Update()
    {
        lock (latestDetections)
        {
            foreach (var det in latestDetections)
            {
                Debug.Log($"Class: {det.@class}, Conf: {det.confidence}");
            }
        }
    }
    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            string newJson = "{\"array\":" + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper.array;
        }

        [System.Serializable]
        private class Wrapper<T>
        {
            public T[] array;
        }
    }

}
