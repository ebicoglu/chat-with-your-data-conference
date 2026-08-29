using Talk.Web.Components;
using OpenAI;
using OpenAI.Realtime;
using Talk.Web.Services;
using Talk.Web.Services.ChartService;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents(o => o.DetailedErrors = true);

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var openAiClient = new OpenAIClient(apiKey);

builder.Services.AddSingleton(new RealtimeClient(apiKey));

// Text-based chat client used for generating SQL from the typed user input.
var chatClient = openAiClient.GetChatClient("gpt-5.4");
builder.Services.AddSingleton(chatClient);
builder.Services.AddSingleton<AiService>();
builder.Services.AddSingleton<RetryService>();

// Chart services
builder.Services.AddSingleton<VegaChartGenerator>();
builder.Services.AddSingleton<ChartRecommendationService>();
builder.Services.AddSingleton<ChartGenerationService>();

var app = builder.Build();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
