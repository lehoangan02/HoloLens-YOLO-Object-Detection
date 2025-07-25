using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OBSFeed : MonoBehaviour
{
    WebCamTexture webcam;

    void Start()
    {
        foreach (var d in WebCamTexture.devices)
            Debug.Log($"WebCam device: {d.name}");
        webcam = new WebCamTexture("OBS Virtual Camera", 640, 640);
        Renderer renderer = GetComponent<Renderer>();
        renderer.material.mainTexture = webcam;
        webcam.Play();
    }

}
