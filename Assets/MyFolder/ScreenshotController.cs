using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ScreenshotController : MonoBehaviour
{
    private UnityEngine.Windows.WebCam.PhotoCapture photoCaptureObject = null;
    void Start()
    {
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.X))
        {
            Debug.Log("Starting Photo Capture...");
            if (photoCaptureObject != null)
            {
                Debug.LogWarning("PhotoCapture already in progress!");
                return;
            }
            UnityEngine.Windows.WebCam.PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            PrintFilePath();
        }
    }
    void OnPhotoCaptureCreated(UnityEngine.Windows.WebCam.PhotoCapture captureObject)
    {
        Debug.Log("OnPhotoCaptureCreated called");

        if (captureObject == null)
        {
            Debug.LogError("PhotoCapture object is null!");
            return;
        }
        photoCaptureObject = captureObject;

        var supportedResolutions = UnityEngine.Windows.WebCam.PhotoCapture.SupportedResolutions;
        if (supportedResolutions == null || !supportedResolutions.Any())
        {
            Debug.LogError("No supported resolutions found!");
            return;
        }

        Resolution cameraResolution = UnityEngine.Windows.WebCam.PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
        Debug.Log($"Selected resolution: {cameraResolution.width}x{cameraResolution.height}");


        UnityEngine.Windows.WebCam.CameraParameters c = new UnityEngine.Windows.WebCam.CameraParameters();
        c.hologramOpacity = 0.0f;
        c.cameraResolutionWidth = cameraResolution.width;
        c.cameraResolutionHeight = cameraResolution.height;
        c.pixelFormat = UnityEngine.Windows.WebCam.CapturePixelFormat.BGRA32;

        Debug.Log("Starting photo mode...");
        captureObject.StartPhotoModeAsync(c, OnPhotoModeStarted);
    }
    void OnStoppedPhotoMode(UnityEngine.Windows.WebCam.PhotoCapture.PhotoCaptureResult result)
    {
        Debug.Log("Photo mode stopped");
        photoCaptureObject.Dispose();
        photoCaptureObject = null;
    }
    private void OnPhotoModeStarted(UnityEngine.Windows.WebCam.PhotoCapture.PhotoCaptureResult result)
    {
        Debug.Log($"Photo mode started. Success: {result.success}");

        if (result.success)
        {
            string filename = string.Format(@"CapturedImage{0}_n.jpg", Time.time);
            string filePath = System.IO.Path.Combine(Application.persistentDataPath, filename);
            Debug.Log($"Saving to: {filePath}");


            photoCaptureObject.TakePhotoAsync(filePath, UnityEngine.Windows.WebCam.PhotoCaptureFileOutputFormat.JPG, OnCapturedPhotoToDisk);
        }
        else
        {
            Debug.LogError("Unable to start photo mode!");
        }
    }
    void OnCapturedPhotoToDisk(UnityEngine.Windows.WebCam.PhotoCapture.PhotoCaptureResult result)
    {
        Debug.Log($"Photo capture completed. Success: {result.success}");

        if (result.success)
        {
            Debug.Log("Saved Photo to disk!");
            photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
        }
        else
        {
            Debug.Log("Failed to save Photo to disk");
        }
    }
    private void PrintFilePath()
    {
        // Debug.Log($"Photo saved to: {System.IO.Path.Combine(Application.persistentDataPath, $"CapturedImage{Time.time}_n.jpg")}");
        string filename = string.Format(@"CapturedImage{0}_n.jpg", Time.time);
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, filename);
        Debug.Log($"Saving to: {filePath}");
    }
    public void TakeScreenShot()
    {
        Debug.Log("TakeScreenShot called");
        UnityEngine.Windows.WebCam.PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
    }
}
