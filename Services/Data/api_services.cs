using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
public class ApiService
{
    private readonly HttpClient _httpClient;

    public ApiService()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri("https://api.example.com/");
    }

   
}