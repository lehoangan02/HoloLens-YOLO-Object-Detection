using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Screamer : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject DiaglogText;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    public void Scream(string message)
    {
        Debug.Log("Screaming: " + message);
    }
     public void ScreamToDialog(string message)
    {
        Debug.Log("Screamed to dialog");
        if (DiaglogText != null)
        {
            // Fetch the TextMeshPro component from the GameObject
            TextMeshPro textMeshPro = DiaglogText.GetComponent<TextMeshPro>();
            if (textMeshPro != null)
            {
                // Set the text directly
                textMeshPro.text = message;
            }
            else
            {
                Debug.LogError("TextMeshPro component not found on DiaglogText GameObject.");
            }
        }
        else
        {
            Debug.LogError("DiaglogText GameObject is not assigned.");
        }
    }
}
