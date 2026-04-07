using UnityEngine;
using TMPro;
using Microsoft.MixedReality.GraphicsTools;

public class NutritionLabelController : MonoBehaviour
{
    [SerializeField] private Canvas worldCanvas;
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
    [SerializeField] private Material materialWhite;
    [SerializeField] private CanvasElementRoundedRect canvasElementRoundedRectCalories;
    [SerializeField] private CanvasElementRoundedRect canvasElementRoundedRectFat;
    [SerializeField] private CanvasElementRoundedRect canvasElementRoundedRectSaturates;
    [SerializeField] private CanvasElementRoundedRect canvasElementRoundedRectSugar;
    [SerializeField] private CanvasElementRoundedRect canvasElementRoundedRectSalt;
    [SerializeField] private BoxCollider hitCollider;
    [SerializeField] private Vector3 hitColliderPadding = new Vector3(0.03f, 0.03f, 0f);
    [SerializeField] private float hitColliderDepth = 0.04f;

    private Vector3 targetPosition;
    private bool positionInitialized;
    private bool hitColliderInitialized;
    private const float LerpSpeed = 8f;

    enum NutrientLevel { BAD, CONCERNING, RESONABLE }

    public string CurrentFoodName =>
        foodNameTextMeshPro != null && !string.IsNullOrWhiteSpace(foodNameTextMeshPro.text)
            ? foodNameTextMeshPro.text
            : gameObject.name;

    void Awake()
    {
        EnsureHitCollider();
        RefreshHitCollider();
    }

    void Update()
    {
        if (positionInitialized && transform.position != targetPosition)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * LerpSpeed);
            lineRenderer.SetPosition(0, objectCenter.transform.position);
            lineRenderer.SetPosition(1, transform.position);
        }

        if (!hitColliderInitialized)
            RefreshHitCollider();
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
        RefreshHitCollider();
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

        SetMaterial(canvasElementRoundedRectCalories,  materialWhite);
        SetMaterial(canvasElementRoundedRectFat,       LevelToMaterial(GetNutriScore("Fat",       foodItem.Nutrition.Fat,       foodItem.ServingType)));
        SetMaterial(canvasElementRoundedRectSaturates, LevelToMaterial(GetNutriScore("Saturates", foodItem.Nutrition.Saturates, foodItem.ServingType)));
        SetMaterial(canvasElementRoundedRectSugar,     LevelToMaterial(GetNutriScore("Sugar",     foodItem.Nutrition.Sugar,     foodItem.ServingType)));
        SetMaterial(canvasElementRoundedRectSalt,      LevelToMaterial(GetNutriScore("Salt",      foodItem.Nutrition.Salt,      foodItem.ServingType)));
    }

    private static void SetMaterial(CanvasElementRoundedRect element, Material mat)
    {
        if (element == null) { Debug.LogError($"CanvasElementRoundedRect is not assigned."); return; }
        element.material = mat;
        element.SetAllDirty();
    }

    private void EnsureHitCollider()
    {
        if (worldCanvas == null)
            worldCanvas = GetComponentInChildren<Canvas>(includeInactive: true);

        if (hitCollider == null)
            hitCollider = GetComponent<BoxCollider>();
        if (hitCollider == null)
            hitCollider = gameObject.AddComponent<BoxCollider>();

        hitCollider.isTrigger = true;
    }

    private void RefreshHitCollider()
    {
        if (hitCollider == null)
            return;

        if (worldCanvas == null)
            worldCanvas = GetComponentInChildren<Canvas>(includeInactive: true);

        Bounds bounds = worldCanvas != null
            ? RectTransformUtility.CalculateRelativeRectTransformBounds(transform, worldCanvas.transform)
            : RectTransformUtility.CalculateRelativeRectTransformBounds(transform);

        if (bounds.size == Vector3.zero)
            return;

        hitCollider.center = bounds.center;
        hitCollider.size = new Vector3(
            Mathf.Max(bounds.size.x + hitColliderPadding.x, 0.01f),
            Mathf.Max(bounds.size.y + hitColliderPadding.y, 0.01f),
            Mathf.Max(hitColliderDepth + hitColliderPadding.z, 0.01f)
        );
        hitColliderInitialized = true;
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
