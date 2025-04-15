using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Zooper.Cheetah.Generators.AzureServiceBus;

[Generator]
public sealed class ChannelGenerator : IIncrementalGenerator
{
	private const string FileName = "MassTransitChannelRegistration";
	private const string BaseNamespace = "Zooper.Cheetah.Integrations.AzureServiceBus";
	private const string NamespaceSuffix = "Channels";
	private const string ClassName = "MassTransitChannelRegistration";
	private const string MethodName = "ConfigureChannels";
	private const string ChannelAttributeName = "Zooper.Cheetah.Attributes.ChannelAttribute";

	// Diagnostic descriptors for logging
	private static readonly DiagnosticDescriptor ExecutionInfo = new(
		"ZER001",
		"Generator Execution",
		"{0}",
		"Generation",
		DiagnosticSeverity.Info,
		true);

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

	/// <summary>
	/// Executes the generation logic.
	/// </summary>
	private static void Execute(
		Compilation compilation,
		ImmutableArray<INamedTypeSymbol> channels,
		SourceProductionContext context)
	{
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, "=== EXECUTING CHANNEL GENERATOR ==="));
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Compilation: {compilation.AssemblyName}"));
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Found {channels.Length} channel(s)"));

		if (channels.IsDefaultOrEmpty)
		{
			context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, "No channels found"));
			return;
		}

		// Get project namespace from compilation
		string projectNamespace = GetFullNamespace();
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Using namespace: {projectNamespace}"));

		// Collect all channel information
		var channelInfos = new List<ChannelInfo>();

		foreach (var classSymbol in channels.Distinct())
		{
			context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Processing channel: {classSymbol.ToDisplayString()}"));

			// Retrieve the ChannelAttribute data
			var attributeData = classSymbol.GetAttributes()
				.FirstOrDefault(ad => ad.AttributeClass?.ToDisplayString() == ChannelAttributeName);

			if (attributeData == null)
			{
				context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"No Channel attribute found on {classSymbol.ToDisplayString()}"));
				continue;
			}

			if (attributeData.ConstructorArguments.Length == 0)
			{
				context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Invalid attribute arguments on {classSymbol.ToDisplayString()}"));
				continue;
			}

			// Extract channel name from the ChannelAttribute
			var channelName = attributeData.ConstructorArguments[0].Value as string;

			if (string.IsNullOrEmpty(channelName))
			{
				context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Invalid channel name on {classSymbol.ToDisplayString()}"));
				continue;
			}

			context.ReportDiagnostic(Diagnostic.Create(
				ExecutionInfo, 
				Location.None, 
				$"Found valid channel: {classSymbol.ToDisplayString()} with channel: {channelName}"));

			channelInfos.Add(
				new ChannelInfo
				{
					EventName = classSymbol.ToDisplayString(),
					ChannelName = channelName
				}
			);
		}

		if (channelInfos.Count == 0)
		{
			context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, "No valid channels found to generate code for"));
			return;
		}

		// Generate the channel registration code
		var sourceBuilder = new StringBuilder();
		AppendUsings(sourceBuilder);
		AppendNamespace(sourceBuilder, projectNamespace);
		AppendClass(sourceBuilder, channelInfos);

		var source = sourceBuilder.ToString();
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Generated source for {channelInfos.Count} channel(s)"));

		// Add the generated source to the compilation
		context.AddSource($"{FileName}.g.cs", SourceText.From(source, Encoding.UTF8));
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Added source file: {FileName}.g.cs"));
	}

	/// <summary>
	/// Gets the full namespace for the generated code
	/// </summary>
	private static string GetFullNamespace()
	{
		return $"{BaseNamespace}.{NamespaceSuffix}";
	}

	private static void AppendUsings(StringBuilder sourceBuilder)
	{
		sourceBuilder.AppendLine("// <auto-generated/>");
		sourceBuilder.AppendLine("using System;");
		sourceBuilder.AppendLine("using System.Collections.Generic;");
		sourceBuilder.AppendLine("using MassTransit;");
		sourceBuilder.AppendLine();
	}

	private static void AppendNamespace(StringBuilder sourceBuilder, string projectNamespace)
	{
		sourceBuilder.AppendLine($"namespace {projectNamespace};");
		sourceBuilder.AppendLine();
	}

	private static void AppendClass(
		StringBuilder sourceBuilder,
		List<ChannelInfo> channels)
	{
		sourceBuilder.AppendLine($"public static class {ClassName}");
		sourceBuilder.AppendLine("{");
		AppendMethod(sourceBuilder, channels);
		sourceBuilder.AppendLine("}");
	}

	private static void AppendMethod(
		StringBuilder sourceBuilder,
		List<ChannelInfo> channels)
	{
		sourceBuilder.AppendLine($"    public static void {MethodName}(IServiceBusBusFactoryConfigurator configurator)");
		sourceBuilder.AppendLine("    {");
		AppendChannelList(sourceBuilder, channels);
		sourceBuilder.AppendLine("    }");
	}

	private static void AppendChannelList(
		StringBuilder sourceBuilder,
		List<ChannelInfo> channels)
	{
		foreach (var channel in channels)
		{
			AppendChannelRegistration(sourceBuilder, channel);
		}
	}

	private static void AppendChannelRegistration(
		StringBuilder sourceBuilder,
		ChannelInfo channel)
	{
		sourceBuilder.AppendLine($"        configurator.ReceiveEndpoint(\"{channel.ChannelName}\", e =>");
		sourceBuilder.AppendLine("        {");
		sourceBuilder.AppendLine($"            e.ConfigureConsumeTopology = false;");
		sourceBuilder.AppendLine("        });");
	}

	/// <summary>
	/// Represents information about a channel.
	/// </summary>
	private class ChannelInfo
	{
		public string EventName { get; set; } = string.Empty;
		public string ChannelName { get; set; } = string.Empty;
	}
}