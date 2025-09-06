using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.GraphicsTools;
using TMPro;

public class UtilityPanelController : MonoBehaviour
{
    private CanvasElementRoundedRect canvasElementRoundedRect;
    private GameObject content;
    [SerializeField]
    private TextMeshPro textBox;
    [SerializeField]
    private UDPClient udpClient;
    void Start()
    {
        canvasElementRoundedRect = GetComponent<CanvasElementRoundedRect>();
        if (canvasElementRoundedRect == null)
        {
            Debug.LogError("No CanvasElementRoundedRect component found!");
        }
        content = transform.Find("Content").gameObject;
        if (content == null)
        {
            Debug.LogError("No Content child object found!");
        }
        if (textBox == null)
        {
            Debug.LogError("No TextBox child object found!");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void MakeTransparent()
    {
        canvasElementRoundedRect.color = new Color(1, 1, 1, 0.0f);
    }
    public void MakeOpaque()
    {
        canvasElementRoundedRect.color = new Color(1, 1, 1, 1.0f);
    }
    private void DisableContent()
    {
        content.SetActive(false);
    }
    private void EnableContent()
    {
        content.SetActive(true);
    }
    public void ToggleTransparency()
    {
        if (canvasElementRoundedRect.color.a == 1.0f)
        {
            MakeTransparent();
        }
        else
        {
            MakeOpaque();
        }
    }
    private void ToggleContent()
    {
        if (content.activeSelf)
        {
            DisableContent();
        }
        else
        {
            EnableContent();
        }
    }
    public void ToogleVisibility()
    {
        ToggleTransparency();
        ToggleContent();
    }
    private void SetText(string text)
    {
        textBox.text = text;
    }
    public void DisplayMyIPAdress()
    {
        string ipAddress = udpClient.LogIPAddress();
        SetText($"IP: {ipAddress}");
    }
}
