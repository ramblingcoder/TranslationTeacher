using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using KristofferStrube.Blazor.FileAPI;
using KristofferStrube.Blazor.MediaCaptureStreams;
using KristofferStrube.Blazor.WebIDL;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI;
using OpenAI.Chat;

namespace TranslationTeacherWasm;

public class Program
{
    [Experimental("SKEXP0010")]
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Configuration.AddJsonFile("appsettings.json");
        builder.Configuration.AddEnvironmentVariables();
        
        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

        // Load Whisper API URL from configuration
        builder.Services.AddScoped<WhisperApiClient>(sp => new WhisperApiClient(builder.Configuration["WhisperApiUrl"] ?? "http://localhost:9000", null));
        builder.Services.AddScoped<ParlerApiClient>(sp => new ParlerApiClient(builder.Configuration["ParlerApiUrl"] ?? "http://localhost:8000"));
        
        builder.Services.AddKeyedScoped<IChatCompletionService>("npc",(sp,_) => 
            new OpenAIChatCompletionService("npc",  new Uri(builder.Configuration["NpcApiUrl"] ?? "http://localhost:8082")));
        
        builder.Services.AddKeyedScoped<IChatCompletionService>("translator",(sp,_) => 
            new OpenAIChatCompletionService("translator",  new Uri(builder.Configuration["TranslatorApiUrl"] ?? "http://localhost:8081")));

        // Adding IMediaDevicesService to service collection.
        builder.Services.AddMediaDevicesService();
        
        builder.Services.AddURLService();

        var app = builder.Build();

        // For Blazor WASM you need to call this to make Error Handling JS Interop.
        await app.Services.SetupErrorHandlingJSInterop();

        await app.RunAsync();
    }
}