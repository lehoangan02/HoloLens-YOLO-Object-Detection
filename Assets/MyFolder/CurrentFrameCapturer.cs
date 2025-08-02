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
        targetIP = "10.0.10.203";
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
        // Get the current frame as pixel data
        Color32[] pixels = WebCamTextureAccess.Instance.WebCamTexture.GetPixels32();
        byte[] byteArray = new byte[pixels.Length * 4];

        // Manually copy the data from Color32[] to byte[]
        for (int i = 0; i < pixels.Length; i++)
        {
            byteArray[i * 4] = pixels[i].r; // Red channel
            byteArray[i * 4 + 1] = pixels[i].g; // Green channel
            byteArray[i * 4 + 2] = pixels[i].b; // Blue channel
            byteArray[i * 4 + 3] = pixels[i].a; // Alpha channel
        }

        // Split the data into smaller chunks
        int maxChunkSize = 65000; // Safe size for UDP packets
        int totalChunks = Mathf.CeilToInt((float)byteArray.Length / maxChunkSize);

        for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
        {
            int offset = chunkIndex * maxChunkSize;
            int chunkSize = Mathf.Min(maxChunkSize, byteArray.Length - offset);

            byte[] chunk = new byte[chunkSize];
            Array.Copy(byteArray, offset, chunk, 0, chunkSize);

            // Send the chunk via UDP
            udpClient.Send(chunk, chunk.Length, endPoint);
        }
    }

    private void OnDestroy()
    {
        // Stop the webcam and close the UDP client
        WebCamTextureAccess.Instance.Stop();
        udpClient.Close();
    }
}