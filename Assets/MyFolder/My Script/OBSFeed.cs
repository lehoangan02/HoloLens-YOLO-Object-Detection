using Assets.Scripts;
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

        // Assign webcam texture to material
        Renderer renderer = GetComponent<Renderer>();
        renderer.material.mainTexture = webcam;

        // Set material to transparent mode so alpha works
        SetMaterialToTransparent();

        // Start disabled (invisible)
        Disable();
    }

    void Enable()
    {
        if (webcam == null)
        {
            Debug.LogError("WebCamTexture is not initialized!");
            return;
        }

        if (!webcam.isPlaying)
            webcam.Play();

        GetComponent<MeshRenderer>().enabled = false;
        Debug.Log("Enabled");
    }

    void Disable()
    {
        GetComponent<MeshRenderer>().enabled = false;
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

    void SetAlpha(float alpha)
    {
        Material mat = GetComponent<Renderer>().material;
        Color color = mat.color;
        color.a = alpha;
        mat.color = color;
    }

    void SetMaterialToTransparent()
    {
        Material mat = GetComponent<Renderer>().material;
        mat.SetFloat("_Mode", 3); // 3 = Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }
}
