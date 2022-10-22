using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class loginResponse
{
        public string accessToken { get; set; }
        public string name { get; set; }
        public int favspace { get; set; }
        public string[] favorites { get; set; }
        public string inviteCode { get; set; }
}

// Root myDeserializedClass = JsonConvert.DeserializeObject<List<Root>>(myJsonResponse);
public class Author
{
    public Id _id { get; set; }
    public string name { get; set; }
    public string link { get; set; }
    public string title { get; set; }
    public string description { get; set; }
    public string sponsor { get; set; }
    public AuthorImage authorImage { get; set; }
    public CreationDate creationDate { get; set; }
    public UpdateDate updateDate { get; set; }
}

public class AuthorImage
{
    public string name { get; set; }
    public string url { get; set; }
}

public class Brand
{
    public string id { get; set; }
    public string name { get; set; }
    public List<string> countries { get; set; }
    public object ingredientTitle { get; set; }
    public object uncountableTitle { get; set; }
    public IngredientImage ingredientImage { get; set; }
}

public class CreationDate
{
    [JsonProperty("$date")]
    public long Date { get; set; }
}

public class De
{
    [JsonProperty("$date")]
    public long Date { get; set; }
}

public class En
{
    [JsonProperty("$date")]
    public long Date { get; set; }
}

public class Id
{
    [JsonProperty("$oid")]
    public string Oid { get; set; }
}

public class Image
{
    public string name { get; set; }
    public string url { get; set; }
    public string type { get; set; }
}

public class ImageList
{
    public string name { get; set; }
    public string url { get; set; }
    public string type { get; set; }
}

public class Ingredient
{
    public Unit unit { get; set; }
    public string ingredientId { get; set; }
    public string title { get; set; }
    public NumberTitle numberTitle { get; set; }
    public Ingredient ingredient { get; set; }
    public double? quantity { get; set; }
    public double? metricQuantity { get; set; }
    public double? quantityUS { get; set; }
    public double? imperialQuantity { get; set; }
    public double? quantityUSProd { get; set; }
    public double? imperialProductQuantity { get; set; }
    public string measure { get; set; }
    public string metricMeasure { get; set; }
    public string measureUS { get; set; }
    public string imperialMeasure { get; set; }
    public string measureUSProd { get; set; }
    public string imperialProductMeasure { get; set; }
}

public class Ingredient3
{
    public Id _id { get; set; }
    public string typ { get; set; }
    public string title { get; set; }
    public NumberTitle numberTitle { get; set; }
    public string uncountableTitle { get; set; }
    public string category { get; set; }
    public string key { get; set; }
    public string desc { get; set; }
    public Image image { get; set; }
    public List<Product> products { get; set; }
    public bool isSponsored { get; set; }
    public Measures measures { get; set; }
    public List<Brand> brands { get; set; }
    public CreationDate creationDate { get; set; }
    public UpdateDate updateDate { get; set; }
    public string note { get; set; }
}

public class IngredientImage
{
    public string name { get; set; }
    public string url { get; set; }
}

public class LocalizedPublishDate
{
    public En en { get; set; }
    public De de { get; set; }
}

public class Measures
{
    public List<string> de { get; set; }
    public List<string> us { get; set; }
}

public class NumberTitle
{
    public string singular { get; set; }
    public string plural { get; set; }
}

public class Product
{
    public Id _id { get; set; }
    public string title { get; set; }
    public string ingredient { get; set; }
    public string retailer { get; set; }
    public double price { get; set; }
    public int quantity { get; set; }
    public double newQuantity { get; set; }
    public string measure { get; set; }
    public CreationDate creationDate { get; set; }
    public UpdateDate updateDate { get; set; }
    public string cbid { get; set; }
}

public class PublishDates
{
    public List<En> en { get; set; }
    public List<De> de { get; set; }
}

public class PublishDuration
{
    public int en { get; set; }
    public int de { get; set; }
}

public class RecipeNutrition
{
    public int calories { get; set; }
    public int protein { get; set; }
    public int fat { get; set; }
    public int carbohydrate { get; set; }
}

public class RecipeRating
{
    public int likeCount { get; set; }
    public int dislikeCount { get; set; }
}

public class Retailer
{
    public Id _id { get; set; }
    public string rkey { get; set; }
    public string key { get; set; }
    public string name { get; set; }
    public string status { get; set; }
    public string typ { get; set; }
    public string country { get; set; }
    public string priceUpdate { get; set; }
    public string mapStatus { get; set; }
    public bool onlineOrderingState { get; set; }
    public CreationDate creationDate { get; set; }
    public UpdateDate updateDate { get; set; }
    public double? discount { get; set; }
}

public class Root
{
    public Id _id { get; set; }
    public string title { get; set; }
    public string rtype { get; set; }
    public string gdocs { get; set; }
    public string authorComment { get; set; }
    public string uid { get; set; }
    public string country { get; set; }
    public int preparationTime { get; set; }
    public int? cookingTime { get; set; }
    public RecipeNutrition recipeNutrition { get; set; }
    public List<string> activeTags { get; set; }
    public List<Step> steps { get; set; }
    public List<Author> authors { get; set; }
    public List<Retailer> retailers { get; set; }
    public List<Ingredient> ingredients { get; set; }
    public List<ImageList> imageList { get; set; }
    public LocalizedPublishDate localizedPublishDate { get; set; }
    public string trackingMode { get; set; }
    public string feature { get; set; }
    public PublishDuration publishDuration { get; set; }
    public int favoriteCount { get; set; }
    public bool modularizationEnabled { get; set; }
    public PublishDates publishDates { get; set; }
    public bool onlineOrderingIsActive { get; set; }
    public RecipeRating recipeRating { get; set; }
    public CreationDate creationDate { get; set; }
    public UpdateDate updateDate { get; set; }
}

public class Step
{
    public string title { get; set; }
    public Image image { get; set; }
    public List<Timer> timers { get; set; }
    public List<Ingredient> ingredients { get; set; }
}

public class Timer
{
    public int minOrExact { get; set; }
    public int? max { get; set; }
}

public class Unit
{
    public double metricQuantity { get; set; }
    public double imperialQuantity { get; set; }
    public double quantity { get; set; }
    public double quantityUS { get; set; }
    public string metricMeasure { get; set; }
    public string imperialMeasure { get; set; }
    public string measure { get; set; }
    public string measureUS { get; set; }
}

public class UpdateDate
{
    [JsonProperty("$date")]
    public long Date { get; set; }
}

