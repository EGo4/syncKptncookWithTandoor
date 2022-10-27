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
using NPOI.POIFS.Crypt;
using System.Security.Policy;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Static variables
        const string identifier = "KptnCook";

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
        Console.WriteLine("Do you want to delete your removed recipes that where added with this account? (CAUTION: THIS IS UNTESTED) (Y/N)");
        bool deleteRecipes = string.Compare(Console.ReadLine(), "Y") == 0 ? true : false;

        // Login to Apis
        Console.WriteLine("Login to Tandor.");        
        string apiKey = $"Bearer {getApiKey(url, tandorUser, tandorPassword)}";

        Console.WriteLine("Login to KptnCook.");
        string[] favorites = await login(username, password);

        // get all the recipes
        Console.WriteLine("Fetching recipes from KptnCook.");
        List<Root> recipes = new List<Root>();
        List<Task<Root>> tasks = new List<Task<Root>>();
        foreach (string favoId in favorites)
            tasks.Add(getRecipe(favoId));

        recipes = (await Task.WhenAll(tasks.ToArray())).ToList();            
        
        // Check for deleted recipes
        if (deleteRecipes)
        {
            Console.WriteLine("Check for deleted recipes by comparing Tandor and KptnCook.");
            List<int> ids = await getIdsOfTandorRecipes(apiKey, url);
            List<string> sourceUrls = await getSourceurlOfTandorRecipes(ids, apiKey, url);
            List<int> deletableIds = getDeletableRecipeIds(ids, sourceUrls, recipes, identifier, username);
            deleteRecipesBasedOnId(deletableIds, apiKey, url);
        }

        // Process existing recipes
        Console.WriteLine($"Found {recipes.Count} recipes. Comparing with Tandor.");
        List<Task<bool>> existingRecipesTask = new List<Task<bool>>();
        recipes.ForEach(recipe => existingRecipesTask.Add(checkRecipeExists(recipe, apiKey, url)));
        List<bool> existingRecipes = (await Task.WhenAll(existingRecipesTask.ToArray())).ToList();
        
        int indexRecipe = 0;
        foreach(bool existingRecipe in existingRecipes)
            if (existingRecipe)
                recipes.RemoveAt(indexRecipe);
            else
                indexRecipe++;

        Console.WriteLine($"Found {recipes.Count} new recipes.");
        
        // Upload new recipes
        if (recipes.Count != 0)
        {
            Console.Write("Processing recipes, this may take a while: ");

            List<Task> uploadTasks = new List<Task>();
            recipes.ForEach(recipe => uploadTasks.Add(importRecipe(recipe, apiKey, url, uploadKeywords, identifier, username)));
            Task.WaitAll(uploadTasks.ToArray());
        }
        Console.WriteLine("Done syncronising KptnCook with Tandor.");
    }

    private static string getApiKey(string url, string user, string pw)
    {
        Configuration configuration = new Configuration();
        configuration.BasePath = url;
        ApiTokenAuthApi tandorAuthApi = new ApiTokenAuthApi(configuration); 
        AccessToken accessToken = tandorAuthApi.CreateAuthToken(user, pw);
        return accessToken.Token;
    }

    private static ApiApi getTandorApi(string apiKey, string url)
    {
        Configuration configuration = new Configuration();
        IDictionary<string, string> dict = new Dictionary<string, string>();
        dict.Add("Authorization", apiKey);
        configuration.DefaultHeader = dict;
        configuration.BasePath = url;
        return new ApiApi(configuration);
    }

    private static async Task<List<int>> getIdsOfTandorRecipes(string apiKey, string url)
    {
        ApiApi tandorApi = getTandorApi(apiKey, url);
        // get list of existing recipes
        ListRecipes200Response? response = await tandorApi.ListRecipesAsync();
        return response.Results.Select(result => result.Id).ToList();
    }

    private static async Task<List<string>> getSourceurlOfTandorRecipes(List<int> ids, string apiKey, string url)
    {
        ApiApi tandorApi = getTandorApi(apiKey, url);
        List<Task<Recipe>> tasks = new List<Task<Recipe>>();
        ids.ForEach(id => tasks.Add(tandorApi.RetrieveRecipeAsync(id.ToString())));
        List<Recipe> recipes = (await Task.WhenAll(tasks.ToArray())).ToList();
        return recipes.Select(recipe => recipe.SourceUrl).ToList();
    }

    private static List<int> getDeletableRecipeIds(List<int> ids, List<string> sourceUrls, List<Root> recipes, string identificationString, string kptnCookEmail)
    {
        List<int> idsToDelete = new List<int>();
        int i = 0;
        while(i < ids.Count())
        {
            if (sourceUrls[i] is null)
            {
                ++i;
                continue;
            }
                

            string[] splittedUrl = sourceUrls[i].Split(" ");

            if (splittedUrl != null
                    && splittedUrl.Length == 2
                    && string.Compare(splittedUrl[0], identificationString, StringComparison.Ordinal) == 0
                    && string.Compare(splittedUrl[1], kptnCookEmail, StringComparison.Ordinal) == 0)
                idsToDelete.Add(ids[i]);

            ++i;
        }

        return idsToDelete;
    }

    private static void deleteRecipesBasedOnId(List<int> idsToDelete, string apiKey, string url)
    {
        ApiApi tandorApi = getTandorApi(apiKey, url);
        List<Task> tasks = new List<Task>();
        idsToDelete.ForEach(async id => await tandorApi.DestroyRecipeAsync(id.ToString()));
        Task.WaitAll(tasks.ToArray());        
    }

    private static async Task<bool> checkRecipeExists(Root recipe, string apiKey, string url)
    {
        ApiApi tandorApi = getTandorApi(apiKey, url);
        // get list of existing recipes
        ListRecipes200Response? response = await tandorApi.ListRecipesAsync();
        List<RecipeOverview> recipesTandor = response is null ? new List<RecipeOverview>() : response.Results;
        // check if recipe is in existing recipes
        return recipesTandor.Any(recipeTandor => recipeTandor.Name == recipe.title); 
    }

    private static async Task importRecipe(Root recipe, string api_key, string url, bool uploadKeywords, string identifier, string kptncookUser)
    {
        ApiApi tandorApi = getTandorApi(api_key, url);
        // components of the tandor recipe
        string name = recipe.title;
        string description = recipe.authorComment;
        List<RecipeKeywordsInner> keywords = new List<RecipeKeywordsInner>();
        if (uploadKeywords)
            recipe.activeTags.ForEach(tag => keywords.Add(new RecipeKeywordsInner(tag, null, "")));
        bool _internal = true;
        
        fetchkptncook.Model.RecipeNutrition recipeNutrition = new fetchkptncook.Model.RecipeNutrition(
            recipe.recipeNutrition.carbohydrate.ToString(),
            recipe.recipeNutrition.fat.ToString(),
            recipe.recipeNutrition.protein.ToString(),
            recipe.recipeNutrition.calories.ToString()
        );
        int workingTime = recipe.preparationTime;
        int waitingTime = recipe.cookingTime ?? 0;
        int servings = 1;
        string filePath = "";

        List<RecipeStepsInner> steps = new List<RecipeStepsInner>();
        int i = 0;
        foreach(Step thisStep in recipe.steps)
        {
            // line 74-85
            // Parse the time and edit instructions accordingly
            int time = 0;
            string instructions = thisStep.title;

            foreach(Timer timer in thisStep.timers)
            {
                time += timer.minOrExact;
                Regex regex = new Regex(Regex.Escape("<timer>"));
                instructions = regex.Replace(instructions, 
                    $"{timer.minOrExact}{(timer.max is null ? "" : " - " + timer.max.ToString())} min", 1);
            }


            List<RecipeStepsInnerIngredientsInner> recipeStepsInnerIngredientsInners = new List<RecipeStepsInnerIngredientsInner>();
            // line 90 - 127
            int orderIng = 0;
            if (thisStep.ingredients != null)
                foreach(Ingredient ingredient in thisStep.ingredients)
                {
                    recipeStepsInnerIngredientsInners.Add(
                        new RecipeStepsInnerIngredientsInner(
                            new IngredientFood(
                                Encoding.UTF8.GetString(Encoding.Default.GetBytes(ingredient.title)),
                                Encoding.UTF8.GetString(Encoding.Default.GetBytes(ingredient.title)),
                                null,
                                "false",
                                null,
                                new List<FoodInheritFieldsInner>(),
                                false,
                                new List<FoodSubstituteInner>(),
                                false,
                                false,
                                new List<FoodInheritFieldsInner>()
                            ),
                            ingredient.unit != null ? ingredient.unit.measure != null ?
                            // line 115,´120, 121
                                new FoodSupermarketCategory(ingredient.unit.metricMeasure) : null :  null,
                            // 114 and 118 and 123
                            ingredient.unit != null ? ingredient.unit.metricQuantity.ToString() : ingredient.quantity != null ? ingredient.metricQuantity.ToString() : "",
                            "",
                            orderIng,
                            false,
                            ingredient.unit == null ? ingredient.quantity == null ? true : false : false,
                            null
                        )
                    );
                    orderIng++;
                }

            //line 61-72
            string imgUrl = thisStep.image.url;
            FileStream imgData = await getImage(imgUrl);
            string[] imgName = System.IO.Path.GetFileName(imgData.Name).Split('.');

            UserFile imageResponse = await tandorApi.CreateUserFileAsync(imgName[0], imgData);
            imgData.Close();
            // line 131
            steps.Add(
                new RecipeStepsInner(
                    "",
                    instructions,
                    recipeStepsInnerIngredientsInners,
                    time,
                    i,
                    true,
                    imageResponse.ToRecipeStepsInnerFile()
                )
            );
            i++;
        }

        Recipe tandorRecipe = new Recipe(name, description, keywords, steps, workingTime, waitingTime, $"{identifier} {kptncookUser}", _internal, true,
            recipeNutrition, servings, filePath, "", false, new List<CustomFilterSharedInner>());
        // line 137
        Recipe responseTandor = await tandorApi.CreateRecipeAsync(tandorRecipe);

        // line 149
        string? coverImgUrl = null;
        foreach (ImageList img in recipe.imageList)
            if(img.type == "cover")
            {
                coverImgUrl = img.url;
                break;
            }

        if(coverImgUrl != null)
        {
            FileStream imgData = await getImage(coverImgUrl);
            RecipeImage recipeImage = tandorApi.ImageRecipe(responseTandor.Id.ToString(), imgData);
            imgData.Close();
        }       
        // End = line 170

    }

    private static async Task<string[]> login(string username, string password)
    {
        HttpClientHandler httpClientHandler = new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
        HttpClient client = new HttpClient(httpClientHandler);

        client.BaseAddress = new Uri("https://mobile.kptncook.com:443");

        var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, new Uri("https://mobile.kptncook.com:443/login/userpass"));
        request.Content = new StringContent($"{{\"email\": \"{username}\", \"password\": \"{password}\"}}", Encoding.ASCII, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        loginResponse? resp = JsonConvert.DeserializeObject<loginResponse>(await response.Content.ReadAsStringAsync());
        string json = await response.Content.ReadAsStringAsync();
        if (resp != null)
            return resp.favorites;
        else
            return new string[] {""};
    }

    private static async Task<FileStream> getImage(string imgUrl)
    {
        HttpClientHandler httpClientHandler = new HttpClientHandler();
        HttpClient client = new HttpClient(httpClientHandler);

        client.DefaultRequestHeaders.Add("kptnkey", "6q7QNKy-oIgk-IMuWisJ-jfN7s6");

        Stream response = await client.GetStreamAsync(imgUrl);

        var debResponse = await client.GetAsync(imgUrl);
        string testString = await debResponse.Content.ReadAsStringAsync();
        // put stream together
        FileStream stream = new FileStream($"{Guid.NewGuid().ToString()}.png", FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4069, FileOptions.DeleteOnClose);
        debResponse.Content.ReadAsStream().CopyTo(stream);
        stream.Position = 0;

        return stream; 

    }

    private static async Task<Root> getRecipe(string id)
    {
        HttpClientHandler httpClientHandler = new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
        HttpClient client = new HttpClient(httpClientHandler);

        client.BaseAddress = new Uri("https://mobile.kptncook.com:443");
        client.DefaultRequestHeaders.Add("hasIngredients", "YES");
        client.DefaultRequestHeaders.Add("kptnkey", "6q7QNKy-oIgk-IMuWisJ-jfN7s6");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.kptncook.mobile-v8+json");
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Platform/Android/5.0.1 App/7.2.7");
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
        client.DefaultRequestHeaders.Add("Connection", "keep-alive");

        var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, new Uri("https://mobile.kptncook.com:443/recipes/search?lang=de&store=de"));
        request.Content = new StringContent("[{ \"identifier\":\"" + id + "\"}]", Encoding.ASCII, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        string? responseString = await response.Content.ReadAsStringAsync();
        List<Root>? resp = JsonConvert.DeserializeObject<List<Root>>(responseString);

        return resp.First() ?? new Root();
    }
}