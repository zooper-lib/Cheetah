using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Zooper.Cheetah.Generators.RabbitMq;

[Generator]
public sealed class ChannelGenerator : IIncrementalGenerator
{
	private const string FileName = "MassTransitChannelRegistration";
	private const string Namespace = "Zooper.Cheetah.Generators.Sample";
	private const string ClassName = "MassTransitChannelRegistration";
	private const string MethodName = "ConfigureChannels";
	private const string ChannelAttributeName = "Zooper.Cheetah.Attributes.ChannelAttribute";

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Register a syntax provider that filters for class declarations with attributes
		var channelTypes = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				ChannelAttributeName,
				(node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
				(context, _) => (INamedTypeSymbol)context.TargetSymbol
			);

		// Combine the compilation with the collected channel symbols
		var compilationAndChannels = context.CompilationProvider.Combine(channelTypes.Collect());

		// Register the source output
		context.RegisterSourceOutput(
			compilationAndChannels,
			(spc, source) => Execute(source.Left, source.Right, spc)
		);
	}

	private static void Execute(
		Compilation compilation,
		ImmutableArray<INamedTypeSymbol> channels,
		SourceProductionContext context)
	{
		context.ReportDiagnostic(Diagnostic.Create(
			new DiagnosticDescriptor(
				"ZER001",
				"Generator Execution",
				"Executing ChannelGenerator",
				"Generation",
				DiagnosticSeverity.Info,
				true),
			Location.None));

		if (channels.IsDefaultOrEmpty)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				new DiagnosticDescriptor(
					"ZER002",
					"No Channels",
					"No channels found to register",
					"Generation",
					DiagnosticSeverity.Info,
					true),
				Location.None));
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(
			new DiagnosticDescriptor(
				"ZER003",
				"Found Channels",
				$"Found {channels.Length} channel(s) to register",
				"Generation",
				DiagnosticSeverity.Info,
				true),
			Location.None));

		// Collect all channel information
		var channelInfos = new List<ChannelInfo>();

		foreach (var classSymbol in channels)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				new DiagnosticDescriptor(
					"ZER004",
					"Processing Channel",
					$"Processing channel: {classSymbol.ToDisplayString()}",
					"Generation",
					DiagnosticSeverity.Info,
					true),
				Location.None));

			var attribute = classSymbol.GetAttributes()
				.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ChannelAttributeName);

			if (attribute == null || attribute.ConstructorArguments.Length == 0)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					new DiagnosticDescriptor(
						"ZER005",
						"Invalid Channel",
						$"No valid Channel attribute found on {classSymbol.ToDisplayString()}",
						"Generation",
						DiagnosticSeverity.Warning,
						true),
					Location.None));
				continue;
			}

			var channelName = attribute.ConstructorArguments[0].Value as string;
			if (string.IsNullOrEmpty(channelName))
			{
				context.ReportDiagnostic(Diagnostic.Create(
					new DiagnosticDescriptor(
						"ZER006",
						"Invalid Channel Name",
						$"Invalid channel name on {classSymbol.ToDisplayString()}",
						"Generation",
						DiagnosticSeverity.Warning,
						true),
					Location.None));
				continue;
			}

			channelInfos.Add(new ChannelInfo
			{
				EventName = classSymbol.ToDisplayString(),
				ChannelName = channelName
			});
		}

		if (channelInfos.Count == 0)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				new DiagnosticDescriptor(
					"ZER007",
					"No Valid Channels",
					"No valid channels found to generate code for",
					"Generation",
					DiagnosticSeverity.Info,
					true),
				Location.None));
			return;
		}

		var sourceBuilder = new StringBuilder();
		AppendUsings(sourceBuilder);
		AppendNamespace(sourceBuilder);
		AppendClass(sourceBuilder, channelInfos);

		var source = sourceBuilder.ToString();
		context.ReportDiagnostic(Diagnostic.Create(
			new DiagnosticDescriptor(
				"ZER008",
				"Generated Source",
				$"Generated source for {channelInfos.Count} channel(s)",
				"Generation",
				DiagnosticSeverity.Info,
				true),
			Location.None));

		context.AddSource($"{FileName}.g.cs", SourceText.From(source, Encoding.UTF8));
	}

	private static void AppendUsings(StringBuilder sourceBuilder)
	{
		sourceBuilder.AppendLine("using MassTransit;");
		sourceBuilder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
		sourceBuilder.AppendLine();
	}

	private static void AppendNamespace(StringBuilder sourceBuilder)
	{
		sourceBuilder.AppendLine($"namespace {Namespace};");
		sourceBuilder.AppendLine();
	}

	private static void AppendClass(StringBuilder sourceBuilder, List<ChannelInfo> channels)
	{
		sourceBuilder.AppendLine($"public static class {ClassName}");
		sourceBuilder.AppendLine("{");
		AppendMethod(sourceBuilder, channels);
		sourceBuilder.AppendLine("}");
	}

	private static void AppendMethod(StringBuilder sourceBuilder, List<ChannelInfo> channels)
	{
		sourceBuilder.AppendLine($"    public static void {MethodName}(this IRabbitMqBusFactoryConfigurator cfg)");
		sourceBuilder.AppendLine("    {");
		AppendChannelList(sourceBuilder, channels);
		sourceBuilder.AppendLine("    }");
	}

	private static void AppendChannelList(StringBuilder sourceBuilder, List<ChannelInfo> channels)
	{
		foreach (var channel in channels)
		{
			AppendChannelRegistration(sourceBuilder, channel);
		}
	}

	private static void AppendChannelRegistration(StringBuilder sourceBuilder, ChannelInfo channel)
	{
		sourceBuilder.AppendLine(
			$"        cfg.Message<{channel.EventName}>(x => x.SetEntityName(\"{channel.ChannelName}\"));"
		);
	}

	private class ChannelInfo
	{
		public string EventName { get; set; } = string.Empty;
		public string ChannelName { get; set; } = string.Empty;
	}
} 