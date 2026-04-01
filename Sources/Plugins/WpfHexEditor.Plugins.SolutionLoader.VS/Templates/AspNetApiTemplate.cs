// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: AspNetApiTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     .NET 8 ASP.NET Core Web API project template.
//     Generates: {name}.sln, {name}/{name}.csproj, {name}/Program.cs,
//                {name}/Controllers/WeatherForecastController.cs,
//                {name}/WeatherForecast.cs
// ==========================================================

namespace WpfHexEditor.Plugins.SolutionLoader.VS.Templates;

/// <summary>.NET 8 ASP.NET Core Web API template.</summary>
internal sealed class AspNetApiTemplate : DotNetProjectTemplate
{
    public override string Id          => "dotnet-webapi";
    public override string DisplayName => "ASP.NET Core Web API (.NET 8)";
    public override string Description => "A .NET 8 RESTful Web API. Generates a ready-to-build .sln + .csproj with a sample controller.";

    protected override Task WriteCsprojAsync(string projectDir, string projectName, CancellationToken ct) =>
        WriteAsync(Path.Combine(projectDir, $"{projectName}.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk.Web">

              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <AssemblyName>{{projectName}}</AssemblyName>
                <RootNamespace>{{projectName}}</RootNamespace>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>

            </Project>
            """, ct);

    protected override async Task WriteSourceFilesAsync(string projectDir, string projectName, CancellationToken ct)
    {
        await WriteAsync(Path.Combine(projectDir, "Program.cs"), $$"""
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
            """, ct);

        await WriteAsync(Path.Combine(projectDir, "WeatherForecast.cs"), $$"""
            namespace {{projectName}};

            public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
            {
                public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
            }
            """, ct);

        var controllersDir = Path.Combine(projectDir, "Controllers");
        Directory.CreateDirectory(controllersDir);

        await WriteAsync(Path.Combine(controllersDir, "WeatherForecastController.cs"), $$"""
            using Microsoft.AspNetCore.Mvc;

            namespace {{projectName}}.Controllers;

            [ApiController]
            [Route("[controller]")]
            public class WeatherForecastController : ControllerBase
            {
                private static readonly string[] _summaries =
                [
                    "Freezing", "Bracing", "Chilly", "Cool", "Mild",
                    "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
                ];

                [HttpGet(Name = "GetWeatherForecast")]
                public IEnumerable<WeatherForecast> Get() =>
                    Enumerable.Range(1, 5).Select(index => new WeatherForecast(
                        DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                        Random.Shared.Next(-20, 55),
                        _summaries[Random.Shared.Next(_summaries.Length)]
                    ));
            }
            """, ct);
    }
}
