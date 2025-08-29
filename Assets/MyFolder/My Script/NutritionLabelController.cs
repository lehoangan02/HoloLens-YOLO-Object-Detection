using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


public class NutritionLabelController : MonoBehaviour
{
    [SerializeField]
    private LineRenderer lineRenderer;
    [SerializeField]
    private GameObject objectCenter;
    [SerializeField]
    private TextMeshProUGUI foodNameTextMeshPro;
    [SerializeField]
    private TextMeshProUGUI servingTextMeshPro;
    [SerializeField]
    private TextMeshProUGUI caloriesTextMeshPro;
    [SerializeField]
    private TextMeshProUGUI fatTextMeshPro;
    [SerializeField]
    private TextMeshProUGUI saturatesTextMeshPro;
    [SerializeField]
    private TextMeshProUGUI sugarTextMeshPro;
    [SerializeField]
    private TextMeshProUGUI saltTextMeshPro;

    void Start()
    {
        
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
    public void SetInfo(FoodItem foodItem)
    {
        foodNameTextMeshPro.text = foodItem.Name;
        servingTextMeshPro.text = foodItem.ServingType;
        caloriesTextMeshPro.text = foodItem.Nutrition.Calories.ToString();
        fatTextMeshPro.text = foodItem.Nutrition.Fat.ToString() + "g";
        saturatesTextMeshPro.text = foodItem.Nutrition.Saturates.ToString() + "g";
        sugarTextMeshPro.text = foodItem.Nutrition.Sugar.ToString() + "g";
        saltTextMeshPro.text = foodItem.Nutrition.Salt.ToString() + "g";

    }
}
