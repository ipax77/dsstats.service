using dsstats.worker;
using dsstats.db;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using dsstats.service;
using dsstats.dbServices;
using System.Net;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Dsstats Service";
});

var sqliteConnectionString = $"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "dsstats.worker", "dsstats4.db")}";
builder.Services.AddDbContext<DsstatsContext>(options => options
    .UseSqlite(sqliteConnectionString, sqlOptions =>
    {
        sqlOptions.MigrationsAssembly("dsstats.migrations.sqlite");
        sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
    })
//.EnableDetailedErrors()
//.EnableSensitiveDataLogging()
);

var uploadUrl = builder.Environment.IsProduction() ? "https://dsstats.pax77.org" : "http://localhost:5279";

builder.Services.AddHttpClient("dsstats")
    .ConfigureHttpClient(options =>
    {
        options.BaseAddress = new Uri(uploadUrl);
        options.DefaultRequestHeaders.Add("Accept", "application/json");
        options.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("DS8upload77");
    }).ConfigurePrimaryHttpMessageHandler(() =>
    new HttpClientHandler
    {
        AutomaticDecompression =
            DecompressionMethods.GZip |
            DecompressionMethods.Brotli,
    });

builder.Services.AddHttpClient("update")
    .ConfigureHttpClient(options =>
    {
        options.BaseAddress = new Uri("https://github.com/ipax77/dsstats.service/releases/latest/download/");
        options.DefaultRequestHeaders.Add("Accept", "application/octet-stream");
    });
builder.Services.AddSingleton<DsstatsService>();
builder.Services.AddSingleton<IImportService, ImportService>();
builder.Services.AddHostedService<WindowsBackgroundService>();

builder.Logging.AddConfiguration(
    builder.Configuration.GetSection("Logging"));

var host = builder.Build();

var dsstatsService = host.Services.GetRequiredService<DsstatsService>();

host.Run();
