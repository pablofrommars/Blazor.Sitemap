Blazor.Sitemap
===========================

[![NuGet](https://img.shields.io/nuget/vpre/Blazor.Sitemap.svg)](https://www.nuget.org/packages/Blazor.Sitemap)
[![NuGet](https://img.shields.io/nuget/dt/Blazor.Sitemap.svg)](https://www.nuget.org/packages/Blazor.Sitemap) 

[Sitemap](https://en.wikipedia.org/wiki/Sitemaps) generator for Blazor.

# Installation

Install [Blazor.Sitemap with NuGet](https://www.nuget.org/packages/Blazor.Sitemap):

    Install-Package Blazor.Sitemap
    
Or via the .NET Core command line interface:

    dotnet add package Blazor.Sitemap

# Usage

## Map endpoint

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllers();
app.MapBlazorHub();
app.MapSitemap("https://pablofrommars.github.io"); //Adjust for your url
app.MapFallbackToPage("/_Host");

app.Run();
```

## Annotate your pages

```csharp
@page "/"
@attribute [SitemapUrl(changeFreq: ChangeFreq.Daily, priority: 1.0)]
```

```csharp
@page "/contact"
@attribute [SitemapUrl(changeFreq: ChangeFreq.Monthly, priority: 0.5)]
```
