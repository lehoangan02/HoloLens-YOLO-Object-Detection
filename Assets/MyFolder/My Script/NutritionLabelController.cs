using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NutritionLabelController : MonoBehaviour
{
    [SerializeField]
    private LineRenderer lineRenderer;
    [SerializeField]
    private GameObject objectCenter;
    void Start()
    {
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void UpdatePosition(Vector3 newPosition)
    {
        this.transform.position = newPosition;
        this.lineRenderer.SetPosition(0, this.objectCenter.transform.position);
        this.lineRenderer.SetPosition(1, this.transform.position);
    }
}
