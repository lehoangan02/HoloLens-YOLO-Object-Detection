using UnityEngine;
using TMPro;
using Microsoft.MixedReality.GraphicsTools;

public class NutritionLabelController : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private GameObject objectCenter;
    [SerializeField] private TextMeshProUGUI foodNameTextMeshPro;
    [SerializeField] private TextMeshProUGUI servingTextMeshPro;
    [SerializeField] private TextMeshProUGUI caloriesTextMeshPro;
    [SerializeField] private TextMeshProUGUI fatTextMeshPro;
    [SerializeField] private TextMeshProUGUI saturatesTextMeshPro;
    [SerializeField] private TextMeshProUGUI sugarTextMeshPro;
    [SerializeField] private TextMeshProUGUI saltTextMeshPro;
    [SerializeField] private Material materialRed;
    [SerializeField] private Material materialGreen;
    [SerializeField] private Material materialYellow;
    [SerializeField] private CanvasElementRoundedRect canvasElementRoundedRectCalories;
    [SerializeField] private CanvasElementRoundedRect canvasElementRoundedRectFat;
    [SerializeField] private CanvasElementRoundedRect canvasElementRoundedRectSaturates;
    [SerializeField] private CanvasElementRoundedRect canvasElementRoundedRectSugar;
    [SerializeField] private CanvasElementRoundedRect canvasElementRoundedRectSalt;

    private Vector3 targetPosition;
    private bool positionInitialized;
    private const float LerpSpeed = 8f;

    enum NutrientLevel { BAD, CONCERNING, RESONABLE }

    void Update()
    {
        if (positionInitialized && transform.position != targetPosition)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * LerpSpeed);
            lineRenderer.SetPosition(0, objectCenter.transform.position);
            lineRenderer.SetPosition(1, transform.position);
        }
    }

    public void UpdatePosition(Vector3 newPosition)
    {
        targetPosition = newPosition;
        if (!positionInitialized)
        {
            transform.position = newPosition;
            positionInitialized = true;
        }
        lineRenderer.SetPosition(0, objectCenter.transform.position);
        lineRenderer.SetPosition(1, transform.position);
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

        canvasElementRoundedRectCalories.material  = LevelToMaterial(GetNutriScore("Calories",   foodItem.Nutrition.Calories,   foodItem.ServingType));
        canvasElementRoundedRectFat.material       = LevelToMaterial(GetNutriScore("Fat",        foodItem.Nutrition.Fat,        foodItem.ServingType));
        canvasElementRoundedRectSaturates.material = LevelToMaterial(GetNutriScore("Saturates",  foodItem.Nutrition.Saturates,  foodItem.ServingType));
        canvasElementRoundedRectSugar.material     = LevelToMaterial(GetNutriScore("Sugar",      foodItem.Nutrition.Sugar,      foodItem.ServingType));
        canvasElementRoundedRectSalt.material      = LevelToMaterial(GetNutriScore("Salt",       foodItem.Nutrition.Salt,       foodItem.ServingType));
    }

    private Material LevelToMaterial(NutrientLevel level) => level switch
    {
        NutrientLevel.RESONABLE  => materialGreen,
        NutrientLevel.CONCERNING => materialYellow,
        _                        => materialRed
    };

    private NutrientLevel GetNutriScore(string nutrient, float value, string servingType)
    {
        if (servingType == "per 100g")
        {
            return nutrient switch
            {
                "Calories"  => value < 100  ? NutrientLevel.RESONABLE : value < 200  ? NutrientLevel.CONCERNING : NutrientLevel.BAD,
                "Fat"       => value < 3    ? NutrientLevel.RESONABLE : value < 17.5f? NutrientLevel.CONCERNING : NutrientLevel.BAD,
                "Saturates" => value < 1.5f ? NutrientLevel.RESONABLE : value < 5    ? NutrientLevel.CONCERNING : NutrientLevel.BAD,
                "Sugar"     => value < 5    ? NutrientLevel.RESONABLE : value < 22.5f? NutrientLevel.CONCERNING : NutrientLevel.BAD,
                "Salt"      => value < 0.3f ? NutrientLevel.RESONABLE : value < 1.5f ? NutrientLevel.CONCERNING : NutrientLevel.BAD,
                _           => NutrientLevel.BAD
            };
        }
        if (servingType == "1 serving")
        {
            return nutrient switch
            {
                "Calories"  => value < 150  ? NutrientLevel.RESONABLE : value < 300 ? NutrientLevel.CONCERNING : NutrientLevel.BAD,
                "Fat"       => value < 5    ? NutrientLevel.RESONABLE : value < 21  ? NutrientLevel.CONCERNING : NutrientLevel.BAD,
                "Saturates" => value < 2    ? NutrientLevel.RESONABLE : value < 6   ? NutrientLevel.CONCERNING : NutrientLevel.BAD,
                "Sugar"     => value < 6    ? NutrientLevel.RESONABLE : value < 27  ? NutrientLevel.CONCERNING : NutrientLevel.BAD,
                "Salt"      => value < 0.4f ? NutrientLevel.RESONABLE : value < 1.8f? NutrientLevel.CONCERNING : NutrientLevel.BAD,
                _           => NutrientLevel.BAD
            };
        }
        return NutrientLevel.BAD;
    }
}
