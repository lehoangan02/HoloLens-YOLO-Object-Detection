using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using Assets.Scripts;

public class LabelReceiver : MonoBehaviour
{
    UdpClient udpClient;
    Thread receiveThread;
    int port = 5011;
    [System.Serializable]
    public class Detection
    {
        public string @class;
        public BBox bbox;
        public float confidence;
        [System.Serializable]
        public class BBox
        {
            public float cx;
            public float cy;
            public float w;
            public float h;
        }
    }
    List<Detection> latestDetections = new List<Detection>();
    List<YoloItem> latestDetectionsYolo = new List<YoloItem>();
    private YoloRecognitionHandler yoloRecognitionHandler;
    private CameraTransform cameraTransform;
    void Start()
    {
        udpClient = new UdpClient(port);
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log($"Listening for YOLO detections on port {port}...");
        yoloRecognitionHandler = gameObject.GetComponent<YoloRecognitionHandler>();
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
                lock (latestDetectionsYolo)
                {
                    latestDetectionsYolo.Clear();
                    for (int i = 0; i < latestDetections.Count; i++)
                    {
                        YoloItem cur = DetectionToYoloV8Item(latestDetections[i]);
                        latestDetectionsYolo.Add(cur);
                    }
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
        this.cameraTransform = new CameraTransform(Camera.main);
        lock (latestDetections)
        {
            foreach (var det in latestDetections)
            {
                //Debug.Log($"Class: {det.@class}, Conf: {det.confidence}, Position: {det.bbox.cx}, {det.bbox.cy}," +
                //    $" Size: {det.bbox.w}, {det.bbox.h}");
            }
        }
        lock (latestDetectionsYolo)
        {

            yoloRecognitionHandler.ShowRecognitions(latestDetectionsYolo,cameraTransform);
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
    private YoloItem DetectionToYoloV8Item(Detection detection)
    {
        return YoloItem.FromVersion8Food(
            new Vector2(detection.bbox.cx, detection.bbox.cy),
            new Vector2(detection.bbox.w, detection.bbox.h),
            detection.confidence,
            detection.@class
        );
    }

}
