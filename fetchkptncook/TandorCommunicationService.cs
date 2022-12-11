using fetchkptncook.Api;
using fetchkptncook.Client;
using fetchkptncook.Model;
using NPOI.POIFS.Crypt.Dsig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace fetchkptncook
{
    public class TandorCommunicationService
    {
        string url;
        string user;
        string password;
        string apiKey;

        public TandorCommunicationService(string _url, string _user, string _password)
        {
            url = _url;
            user = _user;
            password = _password;
            string key = getApiKey() ?? "";
            apiKey = $"Bearer {key}";
        }

        private string? getApiKey()
        {
            Configuration configuration = new Configuration();
            configuration.BasePath = url;
            ApiTokenAuthApi tandorAuthApi = new ApiTokenAuthApi(configuration);
            AccessToken? accessToken = tandorAuthApi.CreateAuthToken(user, password);
            return accessToken.Token;
        }

        private ApiApi getTandorApi(int timeout = 100000)
        {
            Configuration configuration = new Configuration();
            configuration.Timeout = timeout;
            IDictionary<string, string> dict = new Dictionary<string, string>();
            dict.Add("Authorization", apiKey);
            configuration.DefaultHeader = dict;
            configuration.BasePath = url;
            return new ApiApi(configuration);
        }

        public async Task<List<Recipe>> getTandorRecipes(List<int> ids, int maxNumberOfThreads = 10)
        {

            List<Task<Recipe>> tasks = new List<Task<Recipe>>();
            List<Recipe> recipes = new List<Recipe>();
            List<Recipe> currentRecipes = new List<Recipe>();
            ApiApi tandorApi = getTandorApi(10000);

            while (ids.Count() != 0)
            {
                int iTask = 0;

                while (iTask < maxNumberOfThreads && iTask < ids.Count())
                {
                    tasks.Add(tandorApi.RetrieveRecipeAsync(ids[iTask].ToString()));
                    iTask++;
                }
                currentRecipes = (await Task.WhenAll(tasks)).ToList();
                tasks.Clear();
                currentRecipes.ForEach(recipe =>
                {
                    if (recipe is not null)
                    {
                        recipes.Add(recipe);
                        ids.RemoveAll(id => id == recipe.Id);
                    }

                });
            }

            return recipes;
        }

        public List<Recipe> getDeletableRecipeIds(List<Recipe> tandorRecipes, List<Root> recipes, string identificationString, string kptnCookEmail)
        {
            List<int> ids = tandorRecipes.Select(recipe => recipe.Id).ToList();
            List<string> sourceUrls = tandorRecipes.Select(recipe => recipe.SourceUrl).ToList();
            List<Recipe> recipesToDelete = new List<Recipe>();
            int i = 0;
            while (i < ids.Count())
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
                        && string.Compare(splittedUrl[1], kptnCookEmail, StringComparison.Ordinal) == 0
                        && !recipes.Any(recipe => recipe.title.Contains(tandorRecipes[i].Name)))
                    recipesToDelete.Add(tandorRecipes[i]);

                ++i;
            }

            return recipesToDelete;
        }

        public void deleteRecipes(List<Recipe> recipesToDelete)
        {
            ApiApi tandorApi = getTandorApi();
            List<int> imageIdsToDelete = new List<int>();
            List<int> ingredientIdsToDelete = new List<int>();
            List<int> stepIdsToDelete = new List<int>();
            List<Task> tasks = new List<Task>();
            foreach(Recipe recipe in recipesToDelete)
            {
                foreach(RecipeStepsInner step in recipe.Steps)
                {
                    if (!stepIdsToDelete.Any(id => id == step.Id))
                        stepIdsToDelete.Add(step.Id);
                    foreach(RecipeStepsInnerIngredientsInner ingredient in step.Ingredients)
                        if(!ingredientIdsToDelete.Any(id => id == ingredient.Id))
                            ingredientIdsToDelete.Add(ingredient.Id);
                    if(step.File is not null && !imageIdsToDelete.Any(id => id == step.File.Id))
                        imageIdsToDelete.Add(step.File.Id);
                }
            }            
            // Delete ingredients
            ingredientIdsToDelete.ForEach(id => tasks.Add(tandorApi.DestroyIngredientAsync(id.ToString())));
            Task.WaitAll(tasks.ToArray());
            tasks.Clear();
            // Delete steps
            stepIdsToDelete.ForEach(id => tasks.Add(tandorApi.DestroyStepAsync(id.ToString())));
            Task.WaitAll(tasks.ToArray());
            tasks.Clear();
            // Delete recipes
            recipesToDelete.ForEach(recipe => tasks.Add(tandorApi.DestroyRecipeAsync(recipe.Id.ToString())));
            Task.WaitAll(tasks.ToArray());
            tasks.Clear();
            // Delete user files
            imageIdsToDelete.ForEach(id => tasks.Add(tandorApi.DestroyUserFileAsync(id.ToString())));
            Task.WaitAll(tasks.ToArray());
            tasks.Clear();
        }

        public async Task<List<RecipeOverview>> getRecipeOverview()
        {
            ApiApi tandorApi = getTandorApi();
            List<RecipeOverview> recipesTandor = new List<RecipeOverview>();
            ListRecipes200Response response = new ListRecipes200Response();
            // get list of existing recipes (maximum page size is 100)
            int page = 1;
            do
            {
                response = await tandorApi.ListRecipesAsync(
                    null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
                    null, null, null, null, null, null, null, null, null, page, 100);
                response.Results.ForEach(result => recipesTandor.Add(result));
                page++;
            } while (response.Next != null);
            return recipesTandor;
        }

        public async Task<Recipe> uploadRecipe(Recipe recipe)
        {
            ApiApi api = getTandorApi();
            return await api.CreateRecipeAsync(recipe);
        }

        public async Task<UserFile> uploadImage(FileStream imgData)
        {
            ApiApi api = getTandorApi();
            string[] imgName = System.IO.Path.GetFileName(imgData.Name).Split('.');
            UserFile? imgResponse = null;
            while(imgResponse is null)
                try
                {
                    imgResponse = await api.CreateUserFileAsync(imgName[0], imgData);
                }
                catch(Exception ex)
                {
                    imgResponse = null;
                }
                    
            return imgResponse ?? new UserFile();
        }

        public async Task<RecipeImage> uploadCoverImage(string id, FileStream imgData)
        {
            ApiApi api = getTandorApi();
            return await api.ImageRecipeAsync(id, imgData);
        }
    }
}
