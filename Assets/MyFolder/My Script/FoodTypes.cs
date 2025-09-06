using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static FoodTypes;

public class FoodItem
{
    public string Name { get; private set; }
    public string ServingType { get; private set; }
    public Nutrition Nutrition { get; private set; }
    public FoodItem(string Name, string ServingType, Nutrition nutrition)
    {
        this.Nutrition = nutrition;
        this.Name = Name;
        this.ServingType = ServingType;
    }
}
public class FoodTypes : MonoBehaviour
{
    public static FoodTypes Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // This is used to keep object across scenes
        DontDestroyOnLoad(gameObject);
    }
    public class Nutrition
    {
        public int Calories { get; private set;}
        public float Fat {  get; private set;}
        public float Saturates { get; private set;}
        public float Sugar { get; private set;}
        public float Salt { get; private set;}
        public Nutrition(int Calories, float Fat, float Saturates, float Sugar, float Salt)
        {
            this.Calories = Calories;
            this.Fat = Fat;
            this.Saturates = Saturates;
            this.Sugar = Sugar;
            this.Salt = Salt;
        }
    }
    
    public Dictionary<string, FoodItem> Food { get; private set; }
    void Start()
    {
        Food = new Dictionary<string, FoodItem>
        {
            { "Banh canh (Vietnamese thick noodle soup)", new FoodItem("Banh canh (Vietnamese thick noodle soup)", "1 serving", new Nutrition(200, 7, 1.5f, 2, 0.7f)) },
        { "Banh chung (Square sticky rice cake)", new FoodItem("Banh chung (Square sticky rice cake)", "1 serving", new Nutrition(600, 10, 3, 1, 1.2f)) },
        { "Banh cuon (Rolled rice pancake)", new FoodItem("Banh cuon (Rolled rice pancake)", "1 serving", new Nutrition(300, 8, 2, 2, 0.8f)) },
        { "Banh khot (Mini savory pancakes)", new FoodItem("Banh khot (Mini savory pancakes)", "1 serving", new Nutrition(400, 15, 3, 3, 1f)) },
        { "Banh mi (Vietnamese baguette sandwich)", new FoodItem("Banh mi (Vietnamese baguette sandwich)", "1 serving", new Nutrition(350, 12, 4, 4, 1.5f)) },
        { "Banh trang (Rice paper)", new FoodItem("Banh trang (Rice paper)", "per 100g", new Nutrition(300, 0.5f, 0.1f, 0, 0.2f)) },
        { "Banh trang tron (Rice paper salad)", new FoodItem("Banh trang tron (Rice paper salad)", "1 serving", new Nutrition(250, 10, 1.5f, 3, 1f)) },
        { "Banh xeo (Vietnamese sizzling pancake)", new FoodItem("Banh xeo (Vietnamese sizzling pancake)", "1 serving", new Nutrition(400, 16, 4, 3, 1.5f)) },
        { "Bo kho (Beef stew)", new FoodItem("Bo kho (Beef stew)", "1 serving", new Nutrition(450, 20, 8, 5, 2f)) },
        { "Bo la lot (Grilled beef wrapped in betel leaves)", new FoodItem("Bo la lot (Grilled beef wrapped in betel leaves)", "1 serving", new Nutrition(300, 12, 4, 2, 1f)) },
        { "Bong cai (Cauliflower)", new FoodItem("Bong cai (Cauliflower)", "per 100g", new Nutrition(25, 0.1f, 0f, 2, 0f)) },
        { "Bun (Rice vermicelli)", new FoodItem("Bun (Rice vermicelli)", "per 100g", new Nutrition(110, 0.5f, 0.1f, 0.5f, 0.1f)) },
        { "Bun bo Hue (Hue beef noodle soup)", new FoodItem("Bun bo Hue (Hue beef noodle soup)", "1 serving", new Nutrition(500, 15, 5, 4, 2.5f)) },
        { "Bun cha (Grilled pork with vermicelli)", new FoodItem("Bun cha (Grilled pork with vermicelli)", "1 serving", new Nutrition(550, 25, 8, 6, 2.5f)) },
        { "Bun dau (Vermicelli with tofu)", new FoodItem("Bun dau (Vermicelli with tofu)", "1 serving", new Nutrition(400, 20, 3.5f, 4, 1.8f)) },
        { "Bun mam (Fermented fish noodle soup)", new FoodItem("Bun mam (Fermented fish noodle soup)", "1 serving", new Nutrition(600, 20, 5, 6, 2.5f)) },
        { "Bun rieu (Crab noodle soup)", new FoodItem("Bun rieu (Crab noodle soup)", "1 serving", new Nutrition(500, 18, 4, 4, 2.2f)) },
        { "Ca (Fish)", new FoodItem("Ca (Fish)", "per 100g", new Nutrition(150, 4, 1, 0, 0.5f)) },
        { "Ca chua (Tomato)", new FoodItem("Ca chua (Tomato)", "per 100g", new Nutrition(18, 0.2f, 0f, 2.6f, 0f)) },
        { "Ca phao (Pickled eggplant)", new FoodItem("Ca phao (Pickled eggplant)", "per 100g", new Nutrition(30, 0.2f, 0f, 2, 0.6f)) },
        { "Ca rot (Carrot)", new FoodItem("Ca rot (Carrot)", "per 100g", new Nutrition(41, 0.2f, 0f, 4.7f, 0f)) },
        { "Canh (Soup)", new FoodItem("Canh (Soup)", "1 serving", new Nutrition(100, 3, 0.5f, 2, 0.8f)) },
        { "Cha (Vietnamese pork roll)", new FoodItem("Cha (Vietnamese pork roll)", "per 100g", new Nutrition(250, 20, 7, 1, 1.2f)) },
        { "Cha gio (Spring rolls)", new FoodItem("Cha gio (Spring rolls)", "1 serving", new Nutrition(150, 8, 2, 1, 0.5f)) },
        { "Chanh (Lime)", new FoodItem("Chanh (Lime)", "per 100g", new Nutrition(30, 0.2f, 0f, 1.7f, 0.03f)) },
        { "Com (Rice)", new FoodItem("Com (Rice)", "per 100g", new Nutrition(130, 0.3f, 0f, 0, 0f)) },
        { "Com tam (Broken rice)", new FoodItem("Com tam (Broken rice)", "per 100g", new Nutrition(150, 0.5f, 0f, 0, 0.02f)) },
        { "Con nguoi (Human)", new FoodItem("Con nguoi (Human)", "N/A", new Nutrition(0, 0, 0, 0, 0)) },
        { "Cu kieu (Pickled scallion head)", new FoodItem("Cu kieu (Pickled scallion head)", "per 100g", new Nutrition(20, 0.1f, 0f, 1, 1f)) },
        { "Cua (Crab)", new FoodItem("Cua (Crab)", "per 100g", new Nutrition(97, 1.5f, 0.2f, 0, 0.4f)) },
        { "Dau hu (Tofu)", new FoodItem("Dau hu (Tofu)", "per 100g", new Nutrition(76, 4.8f, 0.7f, 0.3f, 0.01f)) },
        { "Dua chua (Pickled vegetables)", new FoodItem("Dua chua (Pickled vegetables)", "per 100g", new Nutrition(25, 0.1f, 0f, 2, 1.5f)) },
        { "Dua leo (Cucumber)", new FoodItem("Dua leo (Cucumber)", "per 100g", new Nutrition(16, 0.1f, 0f, 1.7f, 0f)) },
        { "Goi cuon (Fresh spring rolls)", new FoodItem("Goi cuon (Fresh spring rolls)", "1 serving", new Nutrition(100, 2, 0.5f, 1, 0.5f)) },
        { "Hamburger", new FoodItem("Hamburger", "1 serving", new Nutrition(350, 20, 8, 5, 1.8f)) },
        { "Heo quay (Roast pork)", new FoodItem("Heo quay (Roast pork)", "per 100g", new Nutrition(330, 28, 10, 0, 1.1f)) },
        { "Hu tieu (Clear rice noodle soup)", new FoodItem("Hu tieu (Clear rice noodle soup)", "1 serving", new Nutrition(400, 10, 2, 3, 2f)) },
        { "Kho qua thit (Stuffed bitter melon soup)", new FoodItem("Kho qua thit (Stuffed bitter melon soup)", "1 serving", new Nutrition(250, 7, 2, 2, 1.3f)) },
        { "Khoai tay chien (French fries)", new FoodItem("Khoai tay chien (French fries)", "per 100g", new Nutrition(312, 15, 2, 0.4f, 0.5f)) },
        { "Lau (Hotpot)", new FoodItem("Lau (Hotpot)", "1 serving", new Nutrition(600, 20, 5, 4, 3f)) },
        { "Long heo (Pork offal)", new FoodItem("Long heo (Pork offal)", "per 100g", new Nutrition(250, 20, 7, 0, 0.9f)) },
        { "Mi (Egg noodles)", new FoodItem("Mi (Egg noodles)", "per 100g", new Nutrition(138, 2, 0.5f, 0.2f, 0.6f)) },
        { "Muc (Squid)", new FoodItem("Muc (Squid)", "per 100g", new Nutrition(92, 1.4f, 0.5f, 0, 0.3f)) },
        { "Nam (Mushroom)", new FoodItem("Nam (Mushroom)", "per 100g", new Nutrition(22, 0.1f, 0f, 1.8f, 0.01f)) },
        { "Oc (Snails)", new FoodItem("Oc (Snails)", "per 100g", new Nutrition(79, 1.4f, 0.3f, 0, 0.4f)) },
        { "Ot chuong (Bell pepper)", new FoodItem("Ot chuong (Bell pepper)", "per 100g", new Nutrition(20, 0.2f, 0f, 4.2f, 0.02f)) },
        { "Pho (Vietnamese noodle soup)", new FoodItem("Pho (Vietnamese noodle soup)", "1 serving", new Nutrition(450, 15, 5, 3, 1.8f)) },
        { "Pho mai (Cheese)", new FoodItem("Pho mai (Cheese)", "per 100g", new Nutrition(400, 33, 21, 1.5f, 1.8f)) },
        { "Rau (Vegetables)", new FoodItem("Rau (Vegetables)", "per 100g", new Nutrition(25, 0.2f, 0f, 2, 0.02f)) },
        { "Salad (Salad)", new FoodItem("Salad (Salad)", "1 serving", new Nutrition(150, 10, 2, 2, 0.6f)) },
        { "Thit bo (Beef)", new FoodItem("Thit bo (Beef)", "per 100g", new Nutrition(250, 15, 6, 0, 0.5f)) },
        { "Thit ga (Chicken)", new FoodItem("Thit ga (Chicken)", "per 100g", new Nutrition(239, 13.6f, 3.8f, 0, 0.7f)) },
        { "Thit heo (Pork)", new FoodItem("Thit heo (Pork)", "per 100g", new Nutrition(242, 14, 5, 0, 0.6f)) },
        { "Thit kho (Braised pork)", new FoodItem("Thit kho (Braised pork)", "per 100g", new Nutrition(500, 30, 10, 5, 2f)) },
        { "Thit nuong (Grilled meat)", new FoodItem("Thit nuong (Grilled meat)", "per 100g", new Nutrition(400, 20, 7, 3, 1.5f)) },
        { "Tom (Shrimp)", new FoodItem("Tom (Shrimp)", "per 100g", new Nutrition(99, 1, 0.3f, 0, 0.4f)) },
        { "Trung (Egg)", new FoodItem("Trung (Egg)", "per 100g", new Nutrition(155, 11, 3.3f, 1.1f, 0.12f)) },
        { "Xoi (Sticky rice)", new FoodItem("Xoi (Sticky rice)", "1 serving", new Nutrition(180, 0.3f, 0f, 0.1f, 0.04f)) },
        { "Banh beo (Vietnamese savory steamed rice cake)", new FoodItem("Banh beo (Vietnamese savory steamed rice cake)", "1 serving", new Nutrition(120, 4, 1, 1.5f, 0.3f)) },
        { "Cao lau (Cao lau noodles)", new FoodItem("Cao lau (Cao lau noodles)", "1 serving", new Nutrition(350, 10, 2.5f, 2, 1.4f)) },
        { "Mi Quang (Quang-style noodles)", new FoodItem("Mi Quang (Quang-style noodles)", "1 serving", new Nutrition(400, 12, 3, 3, 1.8f)) },
        { "Com chien duong chau (Yangzhou fried rice)", new FoodItem("Com chien duong chau (Yangzhou fried rice)", "1 serving", new Nutrition(600, 20, 4, 4, 2.2f)) },
        { "Bun cha ca (Fish cake noodle soup)", new FoodItem("Bun cha ca (Fish cake noodle soup)", "1 serving", new Nutrition(500, 15, 4, 3, 1.9f)) },
        { "Com chien ga (Fried rice with chicken)", new FoodItem("Com chien ga (Fried rice with chicken)", "1 serving", new Nutrition(700, 25, 5, 4, 2.5f)) },
        { "Chao long (Pork organ congee)", new FoodItem("Chao long (Pork organ congee)", "1 serving", new Nutrition(400, 10, 2, 1.5f, 1.2f)) },
        { "Nom hoa chuoi (Banana blossom salad)", new FoodItem("Nom hoa chuoi (Banana blossom salad)", "1 serving", new Nutrition(150, 7, 1, 2, 1f)) },
        { "Nui xao bo (Stir-fried macaroni with beef)", new FoodItem("Nui xao bo (Stir-fried macaroni with beef)", "1 serving", new Nutrition(550, 18, 6, 3, 2.1f)) },
        { "Sup cua (Crab soup)", new FoodItem("Sup cua (Crab soup)", "1 serving", new Nutrition(200, 5, 1, 1.5f, 0.9f)) }
        };
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public FoodItem GetFoodItem(string key)
    {
        if (Food.ContainsKey(key))
        {
            return Food[key];
        }
        else
        {
            return null;
        }
    }
}
