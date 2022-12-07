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
using Polly.Caching;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Static variables
        const string identifier = "KptnCook";
        const int maximumTandorThreads = 10;

        // Declare the variables that will be used
        string kptnUsername;
        string kptnPassword;
        string tandorUrl;
        string tandorUser;
        string tandorPassword;
        bool uploadKeywords;
        bool deleteRecipes;
        TandorCommunicationService tandorCommunicationService;
        KptnCookCommunicationService kptnCookCommunicationService;

        // Ask the user what they want to do
        int mode = requestModeFromUser();

        switch (mode)
        {
            case 0:
                string identifierToDelete;
                (tandorUrl, tandorUser, tandorPassword) = getUserInputTandor();
                tandorCommunicationService = await loginToTandorService(tandorUrl, tandorUser, tandorPassword);
                Console.WriteLine("Recipes with which identifier do you want to delete?");
                Console.WriteLine("1 : Every recipe that was imported by KptnCook");
                Console.WriteLine("2 : Only the recipes that where import by a specific KptnCook account");
                switch (Console.ReadLine() ?? "")
                {
                    case "1":
                        identifierToDelete = "KptnCook";
                        break;
                    case "2":
                        Console.WriteLine("Enter the full email of the KptnCook account which recipes you want to delete.");
                        identifierToDelete = Console.ReadLine() ?? "";
                        break;
                    default:
                        Console.WriteLine("No valid identifier specified. Programm will exit now.");
                        return;
                }
                await deleteRecipesBasedOnIdentifier(identifierToDelete, tandorCommunicationService, maximumTandorThreads);
                Console.WriteLine($"Done deleting all recipes with identifier {identifier} from tandor server.");
                break;
            case 1:
                // User input
                (kptnUsername, kptnPassword) = getUserInputKptnCook();
                (tandorUrl, tandorUser, tandorPassword) = getUserInputTandor();
                (uploadKeywords, deleteRecipes) = getUserInputOptions();

                // Login to Apis
                tandorCommunicationService = await loginToTandorService(tandorUrl, tandorUser, tandorPassword);
                kptnCookCommunicationService = await loginToKptnCookService(kptnUsername, kptnPassword);

                // Get all the recipes from KptnCook and Tandor
                List<Root> kptncookRecipes = await fetchRecipesFromKptnCook(kptnCookCommunicationService);
                List<RecipeOverview> currentTandorRecipes = await fetchRecipesFromTandor(tandorCommunicationService);

                // Check for deleted recipes in kptn cook and delete them in tandor on request.
                if (deleteRecipes)
                    currentTandorRecipes = await syncDeletedRecipes(tandorCommunicationService, kptncookRecipes, currentTandorRecipes,
                        maximumTandorThreads, identifier, kptnUsername);

                // Delete duplicates so that they wont get uploaded again
                kptncookRecipes = deleteExistingRecipes(kptncookRecipes, currentTandorRecipes);

                // Upload new recipes
                if (kptncookRecipes.Count != 0)
                    await uploadNewRecipes(tandorCommunicationService, kptnCookCommunicationService, kptncookRecipes,
                        uploadKeywords, identifier, kptnUsername);

                Console.WriteLine("Done synchronising KptnCook with Tandor.");
                break;
            default:
                break;
        }
    }

    // General functions to cluster programm tasks.
    private static int requestModeFromUser()
    {
        Console.WriteLine("What do you want to do?");
        Console.WriteLine("0 : Delete Tandor recipes with a specific identifier.");
        Console.WriteLine("1 : [Default] Import recipes from KptnCook to Tandor.");
        int mode;
        bool result = int.TryParse(Console.ReadLine(), out mode);
        return result ? mode : 1;
    }

    private static async Task deleteRecipesBasedOnIdentifier(string identifier, TandorCommunicationService tandorCommunicationService,
        int maximumTandorThreads)
    {
        List<RecipeOverview> currentTandorRecipes = await fetchRecipesFromTandor(tandorCommunicationService);
        Console.WriteLine("Check for recipes that can be deleted by comparing Tandor sourceUrls and the specified identifier." +
            " This only works if your KptnCook recipes in Tandor where imported by this tool!");
        List<int> ids = currentTandorRecipes.Select(tandorRecipe => tandorRecipe.Id).ToList();
        List<Recipe> tandorRecipes = await tandorCommunicationService.getTandorRecipes(ids, maximumTandorThreads);

        // Extract the ids of all recipes that contain the given identifier
        List<Recipe> deletableRecipes = tandorRecipes.FindAll(tandorRecipe => tandorRecipe.SourceUrl.Contains(identifier)).ToList();
            //.Select(deletableRecipe => deletableRecipe.Id).ToList();
        Console.WriteLine($"Found {deletableRecipes.Count()} recipes that have the identifier {identifier}. Do you want to delete them (Y/N)?");
        if (deletableRecipes.Count() > 0 && string.Compare(Console.ReadLine(), "Y") == 0)
        {
            Console.WriteLine("Start deleting recipes.");
            tandorCommunicationService.deleteRecipes(deletableRecipes);
            Console.WriteLine("Finished deleting recipes.");
        } else
        {
            Console.WriteLine("No recipes where deleted.");
        }
    }

    private static (string, string) getUserInputKptnCook()
    {
        // User input
        Console.WriteLine("Enter KptnCook email:");
        string username = Console.ReadLine() ?? "";
        Console.WriteLine("Enter KptnCook password:");
        string password = Console.ReadLine() ?? "";
        
        return (username, password);
    }

    private static (string, string, string) getUserInputTandor()
    {
        // User input
        Console.WriteLine("Enter Tandor server URL:");
        string url = Console.ReadLine() ?? "";
        Console.WriteLine("Enter Tandor username:");
        string tandorUser = Console.ReadLine() ?? "";
        Console.WriteLine("Enter Tandor password:");
        string tandorPassword = Console.ReadLine() ?? "";

        return (url, tandorUser, tandorPassword);
    }

    private static (bool, bool) getUserInputOptions()
    {
        // User input
        Console.WriteLine("Do you want to import keywords from KptnCook (Y/N)");
        bool uploadKeywords = string.Compare(Console.ReadLine(), "Y") == 0 ? true : false;
        Console.WriteLine("Do you want to delete your removed recipes that where added with this account? (CAUTION: THIS DOES NOT DELETE IMAGES) (Y/N)");
        bool deleteRecipes = string.Compare(Console.ReadLine(), "Y") == 0 ? true : false;

        return (uploadKeywords, deleteRecipes);
    }

    private static async Task<TandorCommunicationService> loginToTandorService(string tandorUrl, string tandorUser, string tandorPassword)
    {
        Console.WriteLine("Login to Tandor.");
        TandorCommunicationService tandorCommunicationService = new TandorCommunicationService(tandorUrl, tandorUser, tandorPassword);

        return tandorCommunicationService;
    }

    private static async Task<KptnCookCommunicationService> loginToKptnCookService(string kptnUsername, string kptnPassword)
    {
        Console.WriteLine("Login to KptnCook.");
        KptnCookCommunicationService kptnCookCommunicationService = await KptnCookCommunicationService.BuildService(kptnUsername, kptnPassword);

        return kptnCookCommunicationService;
    }

    private static async Task<List<Root>> fetchRecipesFromKptnCook(KptnCookCommunicationService kptnCookCommunicationService)
    {
        string[] favorites = kptnCookCommunicationService.favorites ?? new string[0];
        Console.WriteLine("Fetching recipes from KptnCook.");
        List<Task<Root>> tasks = new List<Task<Root>>();
        foreach (string favoId in favorites)
            tasks.Add(kptnCookCommunicationService.getRecipe(favoId));

        List<Root> kptncookRecipes = (await Task.WhenAll(tasks.ToArray())).ToList();
        Console.WriteLine($"Found {kptncookRecipes.Count()} in your KptnCook account.");

        return kptncookRecipes;
    }

    private static async Task<List<RecipeOverview>> fetchRecipesFromTandor(TandorCommunicationService tandorCommunicationService)
    {
        Console.WriteLine("Fetching recipes from Tandor.");
        List<RecipeOverview> currentTandorRecipes = await tandorCommunicationService.getRecipeOverview() ?? new List<RecipeOverview>();
        Console.WriteLine($"Found {currentTandorRecipes.Count()} in your Tandor account.");

        return currentTandorRecipes;
    }

    private static async Task<List<RecipeOverview>> syncDeletedRecipes(TandorCommunicationService tandorCommunicationService, List<Root> kptncookRecipes, 
        List<RecipeOverview> currentTandorRecipes, int maximumTandorThreads, string identifier, string kptnUsername)
    {
        Console.WriteLine("Check for recipes that can be deleted by comparing Tandor sourceUrls and KptnCook. This only works if your KptnCook recipes in Tandor where imported by this tool!");
        List<int> ids = currentTandorRecipes.Select(tandorRecipe => tandorRecipe.Id).ToList();
        List<Recipe> tandorRecipes = await tandorCommunicationService.getTandorRecipes(ids, maximumTandorThreads);

        List<Recipe> deletableRecipes = tandorCommunicationService.getDeletableRecipeIds(tandorRecipes, kptncookRecipes, identifier, kptnUsername);
        Console.WriteLine($"Found {deletableRecipes.Count()} recipes that can be deleted. Do you want to delete them (Y/N)?");
        if (deletableRecipes.Count() > 0 && string.Compare(Console.ReadLine(), "Y") == 0)
        {
            Console.WriteLine("Start deleting recipes.");
            tandorCommunicationService.deleteRecipes(deletableRecipes);
            // Fetch recipes again
            Console.WriteLine("Fetch recipes from Tandor again.");
            currentTandorRecipes = await tandorCommunicationService.getRecipeOverview() ?? new List<RecipeOverview>();
            Console.WriteLine($"Found {currentTandorRecipes.Count()} in your Tandor account.");
        }

        return currentTandorRecipes;
    }

    private static List<Root> deleteExistingRecipes(List<Root> kptncookRecipes, List<RecipeOverview> currentTandorRecipes)
    {
        Console.WriteLine("Check for recipes that already exist in Tandor.");
        int indexRecipe = 0;
        while (indexRecipe < kptncookRecipes.Count())
            if (currentTandorRecipes.Any(recipeTandor => kptncookRecipes[indexRecipe].title.Contains(recipeTandor.Name)))
                kptncookRecipes.RemoveAt(indexRecipe);
            else
                indexRecipe++;

        Console.WriteLine($"Found {kptncookRecipes.Count} new recipes.");

        return kptncookRecipes;
    }

    private static async Task uploadNewRecipes(TandorCommunicationService tandorCommunicationService, KptnCookCommunicationService kptnCookCommunicationService,
        List<Root> kptncookRecipes, bool uploadKeywords, string identifier, string kptnUsername)
    {
        Console.Write("Processing recipes, this may take a while.");

        // Importing all the fetched recipes
        List<Task<Exception?>> uploadTasks = new List<Task<Exception?>>();
        kptncookRecipes.ForEach(recipe => uploadTasks.Add(importRecipe(recipe,
            tandorCommunicationService, kptnCookCommunicationService,
            uploadKeywords, identifier, kptnUsername)));
        List<Exception?> uploadFeedback = (await Task.WhenAll(uploadTasks.ToArray())).ToList();

        // Extracting exceptions
        List<(Exception, int)> exceptions = new List<(Exception, int)>();
        for (int iException = 0; iException < uploadFeedback.Count; ++iException)
            if (uploadFeedback[iException] is not null)
                exceptions.Add((uploadFeedback[iException] ?? new Exception(), iException));


        // Output information about exceptions
        Console.WriteLine($"{kptncookRecipes.Count - exceptions.Count} of {kptncookRecipes.Count} recipes where successfully syncronised.");
        if (exceptions.Count > 0)
        {
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

    // Helper functions to improve readability of the code.
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