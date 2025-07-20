using Microsoft.AspNetCore.Http.Features;
using Radzen;
using Fractality.Client;

namespace Fractality.WebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();
            builder.Services.AddMvc();
            builder.Services.AddRadzenComponents();

            // Get base URLs
            var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
            if (string.IsNullOrEmpty(apiBaseUrl))
            {
                throw new InvalidOperationException("'" + apiBaseUrl + "' is not configured. Please set the ApiBaseUrl configuration in appsettings.json or environment variables.");
            }

            // Add configurations for API URL
            ApiUrl apiUrlConfig = new(apiBaseUrl);

            builder.Services.AddSingleton(apiUrlConfig);

            // Build httpClients
            builder.Services.AddHttpClient<ApiClient>(client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl);
            });

            // Add api clients
            builder.Services.AddScoped(sp =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>();
                return new ApiClient(httpClient.CreateClient("ApiClient"));
            });

            // Allow file uploads > 512 MB
            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 512 * 1024 * 1024;
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            // Use-configs
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseAntiforgery();
            app.UseRouting();

            // Configure endpoints
            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");

            app.MapRazorPages();

            app.Run();
        }
    }

    public class ApiUrl
    {
        public string BaseUrl { get; set; } = string.Empty;

        public ApiUrl(string baseUrl)
        {
            this.BaseUrl = baseUrl;
        }
    }

}
