using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OBSFeed : MonoBehaviour
{
    WebCamTexture webcam;
    private bool isEnabled = false;

    void Start()
    {
        isEnabled = false;
        webcam = WebCamTextureAccess.Instance.WebCamTexture;

        if (webcam == null)
        {
            Debug.LogError("No WebCamTexture available from WebCamTextureAccess!");
            return;
        }

        // Attach the webcam texture to the material (just once is enough)
        Renderer renderer = GetComponent<Renderer>();
        renderer.material.mainTexture = webcam;

        Disable(); // Make the plane transparent at start
    }

    void Enable()
    {
        if (webcam == null)
        {
            Debug.LogError("WebCamTexture is not initialized!");
            return;
        }

        if (!webcam.isPlaying)
        {
            Debug.Log("Attempting to play WebCamTexture...");
            webcam.Play();
            Debug.Log($"WebCamTexture isPlaying: {webcam.isPlaying}");
        }

        Renderer renderer = GetComponent<Renderer>();
        renderer.material.mainTexture = webcam; // <- ✅ This line was missing
        Color color = renderer.material.color;
        color.a = 1f;
        renderer.material.color = color;

        Debug.Log("Enabled");
    }

    void Disable()
    {
        if (webcam != null && webcam.isPlaying)
            webcam.Stop();

        Renderer renderer = GetComponent<Renderer>();
        Color color = renderer.material.color;
        color.a = 0f;
        renderer.material.color = color;

        Debug.Log("Disabled");
    }

    public void Toggle()
    {
        if (!isEnabled)
        {
            Enable();
            isEnabled = true;
        }
        else
        {
            Disable();
            isEnabled = false;
        }
    }
}
