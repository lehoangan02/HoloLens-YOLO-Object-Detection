using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OBSFeed : MonoBehaviour
{
    WebCamTexture webcam;

    void Start()
    {
        var webcam = WebCamTextureAccess.Instance.WebCamTexture;

        if (webcam == null)
        {
            Debug.LogError("No WebCamTexture available from WebCamTextureAccess!");
            return;
        }

        // Attach it to the Renderer’s material
        Renderer renderer = GetComponent<Renderer>();
        renderer.material.mainTexture = webcam;

        // Start playback if not already playing
        if (!webcam.isPlaying)
            webcam.Play();
    }

}
