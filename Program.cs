using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;
using System.Linq.Expressions;
using fetchkptncook;
using fetchkptncook.Api;
using fetchkptncook.Model;
using fetchkptncook.Client;
using System.Collections.Generic;
using NPOI.POIFS.Crypt.Dsig;
using System.IO;
using System;
using System.Text.RegularExpressions;
using NPOI.HPSF;
using System.Security.Policy;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Static variables
        const string identifier = "KptnCook";
        const int maximumTandorThreads = 10;

        // User input
        Console.WriteLine("Enter KptnCook email:");
        string username = Console.ReadLine() ?? "";
        Console.WriteLine("Enter password:");
        string password = Console.ReadLine() ?? "";
        Console.WriteLine("Enter Tandor server URL:");
        string url = Console.ReadLine() ?? "";
        Console.WriteLine("Enter Tandor username:");
        string tandorUser = Console.ReadLine() ?? "";
        Console.WriteLine("Enter Tandor password:");
        string tandorPassword = Console.ReadLine() ?? "";
        Console.WriteLine("Do you want to import keywords from KptnCook (Y/N)");
        bool uploadKeywords = string.Compare(Console.ReadLine(), "Y") == 0 ? true : false;
        Console.WriteLine("Do you want to delete your removed recipes that where added with this account? (CAUTION: THIS DOES NOT DELETE IMAGES) (Y/N)");
        bool deleteRecipes = string.Compare(Console.ReadLine(), "Y") == 0 ? true : false;

        // Login to Apis
        Console.WriteLine("Login to Tandor.");
        TandorCommunicationService tandorCommunicationService = new TandorCommunicationService(url, tandorUser, tandorPassword);

        Console.WriteLine("Login to KptnCook.");
        KptnCookCommunicationService kptnCookCommunicationService = await KptnCookCommunicationService.BuildService(username, password);
        string[] favorites = kptnCookCommunicationService.favorites ?? new string[0];
        
        // get all the recipes from KptnCook
        Console.WriteLine("Fetching recipes from KptnCook.");
        List<Root> recipes = new List<Root>();
        List<Task<Root>> tasks = new List<Task<Root>>();
        foreach (string favoId in favorites)
            tasks.Add(kptnCookCommunicationService.getRecipe(favoId));

        recipes = (await Task.WhenAll(tasks.ToArray())).ToList();
        Console.WriteLine($"Found {recipes.Count()} in your KptnCook account.");
        
        Console.WriteLine("Fetching recipes from Tandor.");
        List<RecipeOverview> currentTandorRecipes = await tandorCommunicationService.getRecipeOverview() ?? new List<RecipeOverview>();
        Console.WriteLine($"Found {currentTandorRecipes.Count()} in your Tandor account.");

        // Check for deleted recipes
        if (deleteRecipes)
        {
            Console.WriteLine("Check for recipes that can be deleted by comparing Tandor sourceUrls and KptnCook. This only works if your KptnCook recipes in Tandor where imported by this tool!");
            List<int> ids = currentTandorRecipes.Select(tandorRecipe => tandorRecipe.Id).ToList();            
            List<Recipe> tandorRecipes = await tandorCommunicationService.getTandorRecipes(ids, maximumTandorThreads);
            
            List<int> deletableIds = tandorCommunicationService.getDeletableRecipeIds(tandorRecipes, recipes, identifier, username);
            Console.WriteLine($"Found {deletableIds.Count()} recipes that can be deleted. Do you want to delete them (Y/N)?");
            if (deletableIds.Count() > 0 && string.Compare(Console.ReadLine(), "Y") == 0)
            {
                Console.WriteLine("Start deleting Recipes.");
                tandorCommunicationService.deleteRecipesBasedOnId(deletableIds);
                // Fetch recipes again
                Console.WriteLine("Fetch recipes from Tandor again.");
                currentTandorRecipes = await tandorCommunicationService.getRecipeOverview() ?? new List<RecipeOverview>();
                Console.WriteLine($"Found {currentTandorRecipes.Count()} in your Tandor account.");

            }

        }

        Console.WriteLine($"Check for recipes that already exist in Tandor.");
        // Process existing recipes
        int indexRecipe = 0;
        while (indexRecipe < recipes.Count())
            if (currentTandorRecipes.Any(
                    recipeTandor => recipes[indexRecipe].title.Contains(recipeTandor.Name)))
                recipes.RemoveAt(indexRecipe);
            else
                indexRecipe++;
       
        Console.WriteLine($"Found {recipes.Count} new recipes.");

        // Upload new recipes
        if (recipes.Count != 0)
        {
            Console.Write("Processing recipes, this may take a while.");

            List<Task<(Exception?, bool)>> uploadTasks = new List<Task<(Exception?, bool)>>();
            recipes.ForEach(recipe => uploadTasks.Add(importRecipe(recipe, uploadKeywords, identifier, username)));
            List<(Exception?, bool)> uploadFeedback = (await Task.WhenAll(uploadTasks.ToArray())).ToList();
        }
        Console.WriteLine("Done syncronising KptnCook with Tandor.");
    }

    private static async Task<(Exception?, bool)> importRecipe(Root recipe, string api_key, string url, bool uploadKeywords, string identifier, string kptncookUser)
    {
        try
        {
            KptnToTandorConverter converter = new KptnToTandorConverter();
            ApiApi tandorApi = getTandorApi(api_key, url);
            // components of the tandor recipe
            string name = recipe.title;
            string description = recipe.authorComment;
            List<RecipeKeywordsInner> keywords = uploadKeywords ? converter.kptnKeywordsToTandorKeywords(recipe.activeTags) : new List<RecipeKeywordsInner>();
            bool _internal = true;
            fetchkptncook.Model.RecipeNutrition recipeNutrition = converter.kptnNutritionToTandorNutrition(recipe.recipeNutrition);
            int workingTime = recipe.preparationTime;
            int waitingTime = recipe.cookingTime ?? 0;
            int servings = 1;
            string filePath = "";

            List<RecipeStepsInner> steps = new List<RecipeStepsInner>();
            int i = 0;
            foreach (Step thisStep in recipe.steps)
            {
                steps.Add(converter.kptnStepToTandorStep(thisStep, i));
                i++;
            }

            Recipe tandorRecipe = new Recipe(name, description, keywords, steps, workingTime, waitingTime, $"{identifier} {kptncookUser}", _internal, true,
                recipeNutrition, servings, filePath, "", false, new List<CustomFilterSharedInner>());

            Recipe responseTandor = await tandorApi.CreateRecipeAsync(tandorRecipe);

            string? coverImgUrl = null;
            foreach (ImageList img in recipe.imageList)
                if (img.type == "cover")
                {
                    coverImgUrl = img.url;
                    break;
                }

            if (coverImgUrl != null)
            {
                FileStream imgData = await getImage(coverImgUrl);
                RecipeImage recipeImage = tandorApi.ImageRecipe(responseTandor.Id.ToString(), imgData);
                imgData.Close();
            }
            // End = line 170
            return (null, true);
        }
        catch(Exception ex)
        {
            return (ex, false);
        }
    }
}