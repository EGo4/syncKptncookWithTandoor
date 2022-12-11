using fetchkptncook;
using fetchkptncook.Model;
using Newtonsoft.Json;
using NPOI.SS.Formula.Functions;

namespace UnitTest_fetchkptncook
{
    [TestClass]
    public class UnitTestKptnToTandorConverter
    {
        [TestMethod]
        public void TestMethod1()
        {
            // Init converter
            KptnToTandorConverter converter = new KptnToTandorConverter();
            // Read file that includes the recipe
            string sCurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string sFile = System.IO.Path.Combine(sCurrentDirectory, @"..\..\..\recipes\Pesto-Risotto_mit_Pfifferlinge.json");
            string sFilePath = Path.GetFullPath(sFile);
            string response = System.IO.File.ReadAllText(sFilePath);
            // Convert string to recipe object
            Root recipe = (JsonConvert.DeserializeObject<List<Root>>(response) ?? new List<Root>()).First();
            // Create input variables
            List<Step> recipeSteps = recipe.steps;
            Step stepToModify = recipeSteps.First();
            List<Ingredient> recipeIngredients = recipe.ingredients;

            RecipeStepsInner? compensationStep = converter.calculateIngredientsCompensationStep(stepToModify, recipeSteps, recipeIngredients);

        }

        [TestMethod]
        public void TestBBQBurger()
        {
            // Init converter
            KptnToTandorConverter converter = new KptnToTandorConverter();
            // Read file that includes the recipe
            string sCurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string sFile = System.IO.Path.Combine(sCurrentDirectory, @"..\..\..\recipes\BBQ-Burger.json");
            string sFilePath = Path.GetFullPath(sFile);
            string response = System.IO.File.ReadAllText(sFilePath);
            // Convert string to recipe object
            Root recipe = (JsonConvert.DeserializeObject<List<Root>>(response) ?? new List<Root>()).First();
            // Create input variables
            List<Step> recipeSteps = recipe.steps;
            Step stepToModify = recipeSteps.First();
            List<Ingredient> recipeIngredients = recipe.ingredients;

            RecipeStepsInner compensationStep = converter.calculateIngredientsCompensationStep(stepToModify, recipeSteps, recipeIngredients) ?? new RecipeStepsInner();
            // Check results
            checkIngredient(compensationStep, "Rohrzucker, unraffiniert", 1, "EL");
            checkIngredient(compensationStep, "Tomaten, passiert", null, null);
            checkIngredient(compensationStep, "vegane Butter", null, null);
        }

        private void checkIngredient(RecipeStepsInner compensationStep, string name, int? amount, string? measure)
        {
            RecipeStepsInnerIngredientsInner? ingredientUnderTest = compensationStep.Ingredients.Find(ing => ing.Food.Name == name);
            if (ingredientUnderTest is not null)
            {
                Assert.AreEqual(amount.ToString() ?? "0", ingredientUnderTest.Amount);
                Assert.AreEqual(measure ?? "", ingredientUnderTest.Unit.Name);
            } else
            {
                Assert.AreEqual(amount, ingredientUnderTest);
            }
        }
    }
}