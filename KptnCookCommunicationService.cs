using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace fetchkptncook;


public class KptnCookCommunicationService
{
    public string[]? favorites;
    string username;
    string password;
    
    public static async Task<KptnCookCommunicationService> BuildService(string _username, string _password)
    {
        KptnCookCommunicationService retVal = new KptnCookCommunicationService(_username, _password);
        retVal.favorites = await retVal.login();
        return retVal;
    }


    public KptnCookCommunicationService(string _username, string _password)
    {
        username = _username;
        password = _password;
    }

    private async Task<string[]> login()
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
            return new string[] { "" };
    }

    public async Task<FileStream> getImage(string imgUrl)
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

    public async Task<Root> getRecipe(string id)
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

        string responseString = await response.Content.ReadAsStringAsync() ?? "";

        List<Root>? resp = JsonConvert.DeserializeObject<List<Root>>(responseString);
        string filename = resp.First().title.Replace(" ", "_").Replace("\"", "").Replace("&", "und");

        //await File.WriteAllLinesAsync($"recipes\\{filename.Substring(0, filename.Length >= 30 ? 30 : filename.Length)}.json", responseString.Split("\\n"));
        return resp.First() ?? new Root();
    }
}
