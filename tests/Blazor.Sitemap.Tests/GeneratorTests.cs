namespace Blazor.Sitemap.Tests;

public class GeneratorTests
{
	[Fact]
	public void Generated_Source()
	{
		var source = @"
using Blazor.Sitemap;
using Microsoft.AspNetCore.Components.RouteAttribute;

namespace Blazor.Sitemap.Tests; 

[Microsoft.AspNetCore.Components.RouteAttribute(""/dummy"")]
[SitemapUrl]
public class Dummy
{
}
";

		var references = AppDomain.CurrentDomain.GetAssemblies()
			.Where(o => !o.IsDynamic)
			.Select(o => MetadataReference.CreateFromFile(o.Location))
			.ToList();

		var compilation = CSharpCompilation.Create(
			assemblyName: "Tests",
			syntaxTrees: new[]
			{
				CSharpSyntaxTree.ParseText(source)
			},
			references: references
		);

		var result = CSharpGeneratorDriver
			.Create(new Generator())
			.RunGenerators(compilation)
			.GetRunResult();

		Assert.True(result.Diagnostics.IsEmpty);

		Assert.Single(result.Results);
		Assert.Equal(3, result.Results[0].GeneratedSources.Length);

		var generated = result.Results[0].GeneratedSources[2].SourceText.ToString();

		Assert.Contains("dummy", generated);
	}
}