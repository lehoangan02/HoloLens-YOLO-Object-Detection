using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using Assets.Scripts;
using System;

public class CurrentFrameCapturer : MonoBehaviour
{
    private UdpClient udpClient;
    private IPEndPoint endPoint;

    public Screamer screamer;

    // IP and port to send the data
    public string targetIP = "10.0.10.203"; // Replace with your target IP
    public int targetPort = 5010; // Replace with your target port

    private void Start()
    {
        targetIP = "192.168.1.7";
        targetPort = 5010;
        // Initialize UDP client and endpoint
        udpClient = new UdpClient();
        endPoint = new IPEndPoint(IPAddress.Parse(targetIP), targetPort);

        // Start the webcam
        WebCamTextureAccess.Instance.Play();

        int imageWidth = WebCamTextureAccess.Instance.WebCamTexture.width;
        int imageHeight = WebCamTextureAccess.Instance.WebCamTexture.height;
        Debug.Log($"Webcam image size: {imageWidth}x{imageHeight}");
        var message = "Webcam image size: " + imageWidth + "x" + imageHeight;
        screamer.ScreamToDialog(message);
    }

    private void Update()
    {
        // Capture and send the current webcam texture frame
        if (WebCamTextureAccess.Instance.WebCamTexture.isPlaying)
        {
            SendCurrentFrame();
        }
    }

    private void SendCurrentFrame()
    {
        // grab dimensions
        int w = WebCamTextureAccess.Instance.WebCamTexture.width;
        int h = WebCamTextureAccess.Instance.WebCamTexture.height;

        // copy pixels into a Texture2D
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(WebCamTextureAccess.Instance.WebCamTexture.GetPixels32());
        tex.Apply();

        // encode to JPEG (quality = 50)
        byte[] jpg = tex.EncodeToJPG(50);

        // send in one go (or chunk if > max UDP size)
        udpClient.Send(jpg, jpg.Length, endPoint);

        // cleanup
        Destroy(tex);
    }

    private void OnDestroy()
    {
        // Stop the webcam and close the UDP client
        WebCamTextureAccess.Instance.Stop();
        udpClient.Close();
    }
}