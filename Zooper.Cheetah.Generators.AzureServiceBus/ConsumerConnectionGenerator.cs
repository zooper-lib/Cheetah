using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.IO;

namespace Zooper.Cheetah.Generators.AzureServiceBus;

[Generator]
public sealed class ConsumerConnectionGenerator : IIncrementalGenerator
{
	private const string FileName = "MassTransitConsumerConnection";
	private const string Namespace = "Zooper.Cheetah.Generators.Sample";
	private const string ClassName = "MassTransitConsumerConnection";
	private const string MethodName = "ConfigureSubscriptions";
	private const string ConsumerAttributeName = "Zooper.Cheetah.Attributes.ConsumerAttribute";

	private static readonly DiagnosticDescriptor InitializationStarted = new(
		id: "ZEA001",
		title: "Generator Initialization",
		messageFormat: "Generator initialization started. Looking for consumers with attribute: {0}",
		category: "Generation",
		DiagnosticSeverity.Info,
		isEnabledByDefault: true);

	private static readonly DiagnosticDescriptor SyntaxNode = new(
		id: "ZEA002",
		title: "Syntax Node",
		messageFormat: "{0}",
		category: "Generation",
		DiagnosticSeverity.Info,
		isEnabledByDefault: true);

	private static readonly DiagnosticDescriptor SemanticTarget = new(
		id: "ZEA003",
		title: "Semantic Target",
		messageFormat: "{0}",
		category: "Generation",
		DiagnosticSeverity.Info,
		isEnabledByDefault: true);

	private static readonly DiagnosticDescriptor ExecutionInfo = new(
		id: "ZEA004",
		title: "Execution",
		messageFormat: "{0}",
		category: "Generation",
		DiagnosticSeverity.Info,
		isEnabledByDefault: true);

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Register a syntax provider that filters for class declarations with attributes
		var consumerClasses = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				"Zooper.Cheetah.Attributes.Consumer",
				(node, _) => node is ClassDeclarationSyntax,
				(context, _) => (INamedTypeSymbol)context.TargetSymbol
			);

		// Combine the compilation with the collected consumer symbols
		var compilationAndConsumers = context.CompilationProvider.Combine(consumerClasses.Collect());

		// Register the source output
		context.RegisterSourceOutput(
			compilationAndConsumers,
			(spc, source) => Execute(source.Left, source.Right, spc)
		);
	}

	private static void Execute(
		Compilation compilation,
		ImmutableArray<INamedTypeSymbol> consumers,
		SourceProductionContext context)
	{
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, "=== EXECUTING GENERATOR ==="));
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Compilation: {compilation.AssemblyName}"));
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Found {consumers.Length} consumer(s)"));

		if (consumers.IsDefaultOrEmpty)
		{
			context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, "No consumers found"));
			return;
		}

		var consumerInfos = new List<ConsumerInfo>();

		foreach (var consumer in consumers)
		{
			context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Processing consumer: {consumer.ToDisplayString()}"));

			var attribute = consumer.GetAttributes()
				.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ConsumerAttributeName ||
								   a.AttributeClass?.ToDisplayString() == "Zooper.Cheetah.Attributes.Consumer");

			if (attribute == null)
			{
				context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"No Consumer attribute found on {consumer.ToDisplayString()}"));
				continue;
			}

			if (attribute.ConstructorArguments.Length < 2)
			{
				context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Invalid attribute arguments on {consumer.ToDisplayString()}"));
				continue;
			}

			var channelName = attribute.ConstructorArguments[0].Value as string;
			var subscriptionName = attribute.ConstructorArguments[1].Value as string;

			if (string.IsNullOrEmpty(channelName) || string.IsNullOrEmpty(subscriptionName))
			{
				context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Invalid attribute values on {consumer.ToDisplayString()}"));
				continue;
			}

			context.ReportDiagnostic(Diagnostic.Create(
				ExecutionInfo, 
				Location.None, 
				$"Found valid consumer: {consumer.ToDisplayString()} with channel: {channelName}, subscription: {subscriptionName}"));

			consumerInfos.Add(new ConsumerInfo
			{
				EventName = consumer.ToDisplayString(),
				ChannelName = channelName,
				SubscriptionName = subscriptionName
			});
		}

		if (consumerInfos.Count == 0)
		{
			context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, "No valid consumers found to generate code for"));
			return;
		}

		var sourceBuilder = new StringBuilder();
		AppendUsings(sourceBuilder);
		AppendNamespace(sourceBuilder);
		AppendClass(sourceBuilder, consumerInfos);

		var source = sourceBuilder.ToString();
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Generated source:\n{source}"));

		context.AddSource($"{FileName}.g.cs", SourceText.From(source, Encoding.UTF8));
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Added source file: {FileName}.g.cs"));
	}

	private static void AppendUsings(StringBuilder sourceBuilder)
	{
		sourceBuilder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
		sourceBuilder.AppendLine("using Microsoft.Extensions.Configuration;");
		sourceBuilder.AppendLine("using System;");
		sourceBuilder.AppendLine("using MassTransit;");
		sourceBuilder.AppendLine();
	}

	private static void AppendNamespace(StringBuilder sourceBuilder)
	{
		sourceBuilder.AppendLine($"namespace {Namespace};");
		sourceBuilder.AppendLine();
	}

	private static void AppendClass(StringBuilder sourceBuilder, List<ConsumerInfo> consumers)
	{
		sourceBuilder.AppendLine($"public static class {ClassName}");
		sourceBuilder.AppendLine("{");
		AppendMethod(sourceBuilder, consumers);
		sourceBuilder.AppendLine("}");
	}

	private static void AppendMethod(StringBuilder sourceBuilder, List<ConsumerInfo> consumers)
	{
		sourceBuilder.AppendLine($"    public static void {MethodName}(this IServiceBusBusFactoryConfigurator cfg, IBusRegistrationContext context)");
		sourceBuilder.AppendLine("    {");
		AppendAllConsumers(sourceBuilder, consumers);
		sourceBuilder.AppendLine("    }");
	}

	private static void AppendAllConsumers(StringBuilder sourceBuilder, List<ConsumerInfo> consumers)
	{
		foreach (var consumer in consumers)
		{
			AppendConsumer(sourceBuilder, consumer);
		}
	}

	private static void AppendConsumer(StringBuilder sourceBuilder, ConsumerInfo consumer)
	{
		sourceBuilder.AppendLine(
			$$"""
				cfg.SubscriptionEndpoint("{{consumer.SubscriptionName}}", "{{consumer.ChannelName}}", e =>
				{
					e.ConfigureConsumer<{{consumer.EventName}}>(context);
				});
			"""
		);
	}

	private class ConsumerInfo
	{
		public string EventName { get; set; } = string.Empty;
		public string ChannelName { get; set; } = string.Empty;
		public string SubscriptionName { get; set; } = string.Empty;
	}
}