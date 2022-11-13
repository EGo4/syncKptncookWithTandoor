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

            // Importing all the fetched recipes
            List<Task<Exception?>> uploadTasks = new List<Task<Exception?>>();
            recipes.ForEach(recipe => uploadTasks.Add(importRecipe(recipe,
                tandorCommunicationService, kptnCookCommunicationService,
                uploadKeywords, identifier, username)));
            List<Exception?> uploadFeedback = (await Task.WhenAll(uploadTasks.ToArray())).ToList();
            
            // Extracting exceptions
            List<(Exception, int)> exceptions = new List<(Exception, int)>();
            for (int iException = 0; iException < uploadFeedback.Count; ++iException)
                if (uploadFeedback[iException] is not null)
                    exceptions.Add((uploadFeedback[iException] ?? new Exception(), iException));
                    
            
            // Output information about exceptions
            Console.WriteLine($"{recipes.Count - exceptions.Count} of {recipes.Count} recipes where successfully syncronised.");
            if(exceptions.Count > 0) {
                List<string> fileText = new List<string>();
                exceptions.ForEach(exception =>
                    {
                        fileText.Add(exception.ToString());
                        fileText.Add("");
                    }
                );
                string fileName = DateTime.Now.ToString("yyyyMMdd\\_hmmtt") + "_exceptions.txt";
                await File.WriteAllLinesAsync(fileName, fileText);
                Console.WriteLine($"The exceptions where save to {fileName}.");
            }
        }
        Console.WriteLine("Done syncronising KptnCook with Tandor.");
    }

    private static async Task<Exception?> importRecipe(Root recipe, 
        TandorCommunicationService tandorCommunicationService, 
        KptnCookCommunicationService kptnCookCommunicationService,
        bool uploadKeywords, string identifier, string kptncookUser)
    {
        try
        {
            KptnToTandorConverter converter = new KptnToTandorConverter();
            // Create the recipe based on the kptn cook recipe
            Recipe tandorRecipe = converter.kptnRecipeToTandorRecipeWithoutSteps(recipe, uploadKeywords, identifier, kptncookUser);
            // Add steps to the recipe
            int iOrder = 1;
            foreach (Step thisStep in recipe.steps)
            {
                // Convert KptnCook step to Tandor step
                RecipeStepsInner stepToAdd = converter.kptnStepToTandorStep(thisStep, iOrder);
                // Get the image from KptnCook
                FileStream imgData = await kptnCookCommunicationService.getImage(thisStep.image.url); 
                // Upload image to Tandor
                UserFile imgResponse = await tandorCommunicationService.uploadImage(imgData);
                imgData.Close();
                // Add image to the step
                stepToAdd.File = imgResponse.ToRecipeStepsInnerFile();
                // Add step to the recipe
                tandorRecipe.Steps.Add(stepToAdd);
                iOrder++;
            }
            // Check if the used ingredients isequal to the overall ingredients since KptnCook seperates
            // them from each other and tandor only uses ingredients in steps and calculates the overall
            // ingredients based on that.
            RecipeStepsInner? ingredientStep = converter.calculateIngredientsCompensationStep(
                recipe.steps.First(), recipe.steps, recipe.ingredients, 0);
            if (ingredientStep is not null)
                tandorRecipe.Steps = tandorRecipe.Steps.Prepend(ingredientStep).ToList();
            // Upload the fully coverted recipe to tandor
            Recipe responseTandor = await tandorCommunicationService.uploadRecipe(tandorRecipe);
            // Get the cover image from kptn cook recipe
            ImageList? coverImg = recipe.imageList.Find(img => img.type == "cover");
            // Upload the cover image if it was found
            if (coverImg != null)
            {
                // Fetch file data
                FileStream coverImgData = await kptnCookCommunicationService.getImage(coverImg.url);
                RecipeImage recipeImage = await tandorCommunicationService.uploadCoverImage(
                    responseTandor.Id.ToString(), coverImgData);
                coverImgData.Close();
            }

            return null;
        }
        catch(Exception ex)
        {
            return ex;
        }
    }
}