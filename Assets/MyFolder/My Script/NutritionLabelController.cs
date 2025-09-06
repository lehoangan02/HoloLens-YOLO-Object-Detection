using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Microsoft.MixedReality.GraphicsTools;


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
    [SerializeField]
    private Material materialRed;
    [SerializeField]
    private Material materialGreen;
    [SerializeField]
    private Material materialYellow;
    [SerializeField]
    private CanvasElementRoundedRect canvasElementRoundedRectCalories;
    [SerializeField]
    private CanvasElementRoundedRect canvasElementRoundedRectFat;
    [SerializeField]
    private CanvasElementRoundedRect canvasElementRoundedRectSaturates;
    [SerializeField]
    private CanvasElementRoundedRect canvasElementRoundedRectSugar;
    [SerializeField]
    private CanvasElementRoundedRect canvasElementRoundedRectSalt;

    enum NutrientLevel
    {
        BAD,
        CONCERNING,
        RESONABLE
    }

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
        caloriesTextMeshPro.text = foodItem.Nutrition.Calories.ToString() + "kcal";
        fatTextMeshPro.text = foodItem.Nutrition.Fat.ToString() + "g";
        saturatesTextMeshPro.text = foodItem.Nutrition.Saturates.ToString() + "g";
        sugarTextMeshPro.text = foodItem.Nutrition.Sugar.ToString() + "g";
        saltTextMeshPro.text = foodItem.Nutrition.Salt.ToString() + "g";
        // Set color based on nutriscore
        if (GetNutriScoreColor("Calories", foodItem.Nutrition.Calories, foodItem.ServingType) == NutrientLevel.RESONABLE)
        {
            canvasElementRoundedRectCalories.material = materialGreen;
        }
        else if (GetNutriScoreColor("Calories", foodItem.Nutrition.Calories, foodItem.ServingType) == NutrientLevel.CONCERNING)
        {
            canvasElementRoundedRectCalories.material = materialYellow;
        }
        else
        {
            canvasElementRoundedRectCalories.material = materialRed;
        }
        if (GetNutriScoreColor("Fat", foodItem.Nutrition.Fat, foodItem.ServingType) == NutrientLevel.RESONABLE)
        {
            canvasElementRoundedRectFat.material = materialGreen;
        }
        else if (GetNutriScoreColor("Fat", foodItem.Nutrition.Fat, foodItem.ServingType) == NutrientLevel.CONCERNING)
        {
            canvasElementRoundedRectFat.material = materialYellow;
        }
        else
        {
            canvasElementRoundedRectFat.material = materialRed;
        }
        if (GetNutriScoreColor("Saturates", foodItem.Nutrition.Saturates, foodItem.ServingType) == NutrientLevel.RESONABLE)
        {
            canvasElementRoundedRectSaturates.material = materialGreen;
        }
        else if (GetNutriScoreColor("Saturates", foodItem.Nutrition.Saturates, foodItem.ServingType) == NutrientLevel.CONCERNING)
        {
            canvasElementRoundedRectSaturates.material = materialYellow;
        }
        else
        {
            canvasElementRoundedRectSaturates.material = materialRed;
        }
        if (GetNutriScoreColor("Sugar", foodItem.Nutrition.Sugar, foodItem.ServingType) == NutrientLevel.RESONABLE)
        {
            canvasElementRoundedRectSugar.material = materialGreen;
        }
        else if (GetNutriScoreColor("Sugar", foodItem.Nutrition.Sugar, foodItem.ServingType) == NutrientLevel.CONCERNING)
        {
            canvasElementRoundedRectSugar.material = materialYellow;
        }
        else
        {
            canvasElementRoundedRectSugar.material = materialRed;
        }
        if (GetNutriScoreColor("Salt", foodItem.Nutrition.Salt, foodItem.ServingType) == NutrientLevel.RESONABLE)
        {
            canvasElementRoundedRectSalt.material = materialGreen;
        }
        else if (GetNutriScoreColor("Salt", foodItem.Nutrition.Salt, foodItem.ServingType) == NutrientLevel.CONCERNING)
        {
            canvasElementRoundedRectSalt.material = materialYellow;
        }
        else
        {
            canvasElementRoundedRectSalt.material = materialRed;
        }

    }
    private NutrientLevel GetNutriScoreColor(string nutrient, float value, string servingType)
    {
        if (servingType == "per 100g")
        {
            if (nutrient == "Calories")
            {
                if (value < 100) return NutrientLevel.RESONABLE;
                else if (value < 200) return NutrientLevel.CONCERNING;
                else return NutrientLevel.BAD;
            }
            else if (nutrient == "Fat")
            {
                if (value < 3) return NutrientLevel.RESONABLE;
                else if (value < 17.5) return NutrientLevel.CONCERNING;
                else return NutrientLevel.BAD;
                
            }
            else if (nutrient == "Saturates")
            {
                if (value < 1.5) return NutrientLevel.RESONABLE;
                else if (value < 5) return NutrientLevel.CONCERNING;
                else return NutrientLevel.BAD;
            }
            else if (nutrient == "Sugar")
            {
                if (value < 5) return NutrientLevel.RESONABLE;
                else if (value < 22.5) return NutrientLevel.CONCERNING;
                else return NutrientLevel.BAD;
            }
            else if (nutrient == "Salt")
            {
                if (value < 0.3) return NutrientLevel.RESONABLE;
                else if (value < 1.5) return NutrientLevel.CONCERNING;
                else return NutrientLevel.BAD;
            }
        }
        else if (servingType == "1 serving")
        {
            if (nutrient == "Calories")
            {
                if (value < 150) return NutrientLevel.RESONABLE;
                else if (value < 300) return NutrientLevel.CONCERNING;
                else return NutrientLevel.BAD;
                
            }
            else if (nutrient == "Fat")
            {
                if (value < 5) return NutrientLevel.RESONABLE;
                else if (value < 21) return NutrientLevel.CONCERNING;
                else return NutrientLevel.BAD;
                
            }
            else if (nutrient == "Saturates")
            {
                if (value < 2) return NutrientLevel.RESONABLE;
                else if (value < 6) return NutrientLevel.CONCERNING;
                else return NutrientLevel.BAD;
            }
            else if (nutrient == "Sugar")
            {
                if (value < 6) return NutrientLevel.RESONABLE;
                else if (value < 27) return NutrientLevel.CONCERNING;
                else return NutrientLevel.BAD;
            }
            else if (nutrient == "Salt")
            {
                if (value < 0.4) return NutrientLevel.RESONABLE;
                else if (value < 1.8) return NutrientLevel.CONCERNING;
                else return NutrientLevel.BAD;
            }
        }

        return NutrientLevel.BAD;
    }

}
