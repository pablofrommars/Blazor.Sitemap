using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Blazor.Sitemap;

[Generator]
public class Generator : IIncrementalGenerator
{
	private const string changeFreqText = @"namespace Blazor.Sitemap;

public enum ChangeFreq
{
	Always,
	Hourly,
	Daily,
	Weekly,
	Monthly,
	Yearly,
	Never
}";

	private const string sitemapUrlAttributeText = @"using System;

namespace Blazor.Sitemap;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class SitemapUrlAttribute : Attribute
{
	public ChangeFreq ChangeFreq { get; }
	public double Priority { get; }

	public SitemapUrlAttribute(ChangeFreq changeFreq = ChangeFreq.Daily, double priority = 0.5)
	{
		ChangeFreq = changeFreq;
		Priority = priority;
	}
}";

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterPostInitializationOutput(context =>
		{
			context.AddSource("Blazor.Sitemap.ChangeFreq.g.cs", changeFreqText);
			context.AddSource("Blazor.Sitemap.SitemapUrlAttribute.g.cs", sitemapUrlAttributeText);
		});

		var classes = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => Predicate(node),
				transform: static (context, _) => Transform(context))
			.Where(static o => o is not null)
			.Select(static (o, _) => o!);

		var combine = context.CompilationProvider.Combine(classes.Collect());

		context.RegisterSourceOutput(combine,
			static (context, source) => Execute(source.Left, source.Right, context));
	}

	private static bool Predicate(SyntaxNode node)
		=> node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

	private static ClassDeclarationSyntax? Transform(GeneratorSyntaxContext context)
	{
		var @class = (ClassDeclarationSyntax)context.Node;

		var hasRoute = false;
		var hasSitemapUrl = false;

		foreach (var attributeListSyntax in @class.AttributeLists)
		{
			foreach (var attributeSyntax in attributeListSyntax.Attributes)
			{
				var name = attributeSyntax.Name.ToString();

				if (!hasRoute && name == "Microsoft.AspNetCore.Components.RouteAttribute")
				{
					if (hasSitemapUrl)
					{
						return @class;
					}

					hasRoute = true;
				}

				if (!hasSitemapUrl && name == "SitemapUrl")
				{
					if (hasRoute)
					{
						return @class;
					}

					hasSitemapUrl = true;
				}
			}
		}

		return null;
	}

	private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
	{
		var entries = new List<(string template, string changeFreq, double priority)>();

		if (!classes.IsDefaultOrEmpty)
		{
			var routeAttribute = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.RouteAttribute");
			var sitemapURLAttribute = compilation.GetTypeByMetadataName("Blazor.Sitemap.SitemapUrlAttribute");

			foreach (var @class in classes)
			{
				var symbol = (compilation
					.GetSemanticModel(@class.SyntaxTree)
					.GetDeclaredSymbol(@class) as ITypeSymbol)!;

				AttributeData? routeAttributeData = null;
				foreach (var attributeData in symbol.GetAttributes())
				{
					if (attributeData.AttributeClass!.Equals(routeAttribute, SymbolEqualityComparer.Default))
					{
						routeAttributeData = attributeData;
						break;
					}
				}

				if (routeAttributeData is null)
				{
					continue;
				}

				if (routeAttributeData.ConstructorArguments.Length != 1)
				{
					continue;
				}

				if (routeAttributeData.ConstructorArguments[0].Value is not string template)
				{
					continue;
				}

				AttributeData? sitemapURLAttributeData = null;
				foreach (var attributeData in symbol.GetAttributes())
				{
					if (attributeData.AttributeClass!.Equals(sitemapURLAttribute, SymbolEqualityComparer.Default))
					{
						sitemapURLAttributeData = attributeData;
						break;
					}
				}

				if (sitemapURLAttributeData is null)
				{
					continue;
				}

				if (sitemapURLAttributeData.ConstructorArguments.Length != 2)
				{
					continue;
				}

				var changeFreq = (int)sitemapURLAttributeData.ConstructorArguments[0].Value! switch
				{
					1 => "hourly",
					2 => "daily",
					3 => "weekly",
					4 => "monthly",
					5 => "yearly",
					6 => "never",
					_ => "always"
				};

				var priority = Math.Max(0, Math.Min(1, (double)sitemapURLAttributeData.ConstructorArguments[1].Value!));

				entries.Add((template, changeFreq, priority));
			}
		}

		var builder = new StringBuilder();

		builder.AppendLine(@"#nullable enable

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text;
using System.Threading;

namespace Blazor.Sitemap;

public static class IEndpointRouteBuilderExtensions
{
	public static IEndpointConventionBuilder MapSitemap(this IEndpointRouteBuilder endpoints, string url)
		=> endpoints.Map(""sitemap.xml"", (HttpContext context, CancellationToken cancellationToken) =>
		{
			context.Response.ContentType = ""text/xml"";

			return context.Response.WriteAsync(@$""<?xml version=""""1.0"""" encoding=""""utf-8""""?>
<urlset xmlns:xsi=""""http://www.w3.org/2001/XMLSchema-instance"""" xsi:schemaLocation=""""http://www.sitemaps.org/schemas/sitemap/0.9 http://www.sitemaps.org/schemas/sitemap/0.9/sitemap.xsd"""" xmlns=""""http://www.sitemaps.org/schemas/sitemap/0.9"""">");

		foreach (var (template, changeFreq, priority) in entries)
		{
			builder.AppendLine(@$"    <url>
    	<loc>{{url}}{template}</loc>
        <changefreq>{changeFreq}</changefreq>
        <priority>{priority}</priority>
    </url>");
		}

		builder.Append(@"</urlset>"", cancellationToken);
		});
}");

		context.AddSource("Blazor.Sitemap.IEndpointRouteBuilderExtensions.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
	}
}