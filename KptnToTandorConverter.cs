using fetchkptncook.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace fetchkptncook;

public class KptnToTandorConverter
{
    public List<RecipeKeywordsInner> kptnKeywordsToTandorKeywords(List<string> tags)
    {
        List<RecipeKeywordsInner> keywords = new List<RecipeKeywordsInner>();
        tags.ForEach(tag => keywords.Add(new RecipeKeywordsInner(tag, null, "")));
        return keywords;
    }

    public fetchkptncook.Model.RecipeNutrition kptnNutritionToTandorNutrition(RecipeNutrition nutrition)
    {
        return new fetchkptncook.Model.RecipeNutrition(
            nutrition.carbohydrate.ToString(),
            nutrition.fat.ToString(),
            nutrition.protein.ToString(),
            nutrition.calories.ToString()
        );
    }

    public RecipeStepsInner kptnStepToTandorStep(Step step, int order)
    {
        // Parse the time and edit instructions accordingly
        int time = 0;
        string instructions = step.title;

        foreach (Timer timer in step.timers)
        {
            time += timer.minOrExact;
            Regex regex = new Regex(Regex.Escape("<timer>"));
            instructions = regex.Replace(instructions,
                $"{timer.minOrExact}{(timer.max is null ? "" : " - " + timer.max.ToString())} min", 1);
        }

        List<RecipeStepsInnerIngredientsInner> recipeStepsInnerIngredientsInners = new List<RecipeStepsInnerIngredientsInner>();

        int orderIng = 0;
        if (step.ingredients != null)
            foreach (Ingredient ingredient in step.ingredients)
            {
                recipeStepsInnerIngredientsInners.Add(
                    kptnIngredientToTandorIngredient(ingredient, orderIng)
                );
                orderIng++;
            }

        string imgUrl = step.image.url;
        FileStream imgData = await getImage(imgUrl);
        string[] imgName = System.IO.Path.GetFileName(imgData.Name).Split('.');

        UserFile imageResponse = await tandorApi.CreateUserFileAsync(imgName[0], imgData);
        imgData.Close();

        // line 131
        return new RecipeStepsInner(
                "",
                instructions,
                recipeStepsInnerIngredientsInners,
                time,
                order,
                true,
                imageResponse.ToRecipeStepsInnerFile()
        );
    }    
    
    public RecipeStepsInnerIngredientsInner kptnIngredientToTandorIngredient(Ingredient ingredient, int orderIng)
    {
        // create food
        IngredientFood food = new IngredientFood(
                                    ingredient.title,
                                    ingredient.title,
                                    null,
                                    "false",
                                    null,
                                    new List<FoodInheritFieldsInner>(),
                                    false,
                                    new List<FoodSubstituteInner>(),
                                    false,
                                    false,
                                    new List<FoodInheritFieldsInner>()
                                );
        // get the correct unit
        FoodSupermarketCategory? unit = null;
        if (ingredient.unit is not null && ingredient.unit.measure is not null && ingredient.unit.metricMeasure is not null)
            unit = new FoodSupermarketCategory(ingredient.unit.metricMeasure);

        // get the correct amount
        string amount = "";
        if (ingredient.unit is not null)
            amount = ingredient.unit.metricQuantity.ToString();
        else if (ingredient.quantity is not null && ingredient.metricQuantity is not null)
            amount = ingredient.metricQuantity.ToString();
        // Replace character to comply with API, this is undocumented in V1.4.4 but neccesarry.
        amount = amount.Replace(",", ".");

        // set variable that determines whether amount is given
        bool noAmount = amount == "" ? true : false;

        return new RecipeStepsInnerIngredientsInner(food, unit, amount, "", orderIng, false, noAmount, null);
    }

    public Recipe kptnRecipeToTandorRecipeWithoutSteps(Root recipe, bool uploadKeywords, string identifier, string kptncookUser)
    {
        // components of the tandor recipe
        string name = recipe.title;
        string description = recipe.authorComment;
        List<RecipeKeywordsInner> keywords = uploadKeywords ? this.kptnKeywordsToTandorKeywords(recipe.activeTags) : new List<RecipeKeywordsInner>();
        bool _internal = true;
        fetchkptncook.Model.RecipeNutrition recipeNutrition = this.kptnNutritionToTandorNutrition(recipe.recipeNutrition);
        int workingTime = recipe.preparationTime;
        int waitingTime = recipe.cookingTime ?? 0;
        int servings = 1;
        string filePath = "";

        List<RecipeStepsInner> steps = new List<RecipeStepsInner>();

        return new Recipe(name, description, keywords, steps, workingTime, waitingTime, $"{identifier} {kptncookUser}", _internal, true,
            recipeNutrition, servings, filePath, "", false, new List<CustomFilterSharedInner>());
    }
}

