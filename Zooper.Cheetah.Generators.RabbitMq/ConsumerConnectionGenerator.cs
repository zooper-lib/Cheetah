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
public sealed class ConsumerConnectionGenerator : IIncrementalGenerator
{
	private const string FileName = "MassTransitConsumerConnection";
	private const string Namespace = "Zooper.Cheetah.Generators.Sample";
	private const string ClassName = "MassTransitConsumerConnection";
	private const string MethodName = "ConfigureSubscriptions";
	private const string ConsumerAttributeName = "Zooper.Cheetah.Attributes.ConsumerAttribute";

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Register a syntax provider that filters for class declarations with attributes
		var consumerClasses = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				ConsumerAttributeName,
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
		context.ReportDiagnostic(Diagnostic.Create(
			new DiagnosticDescriptor(
				"ZER001",
				"Generator Execution",
				"Executing ConsumerConnectionGenerator",
				"Generation",
				DiagnosticSeverity.Info,
				true),
			Location.None));

		if (consumers.IsDefaultOrEmpty)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				new DiagnosticDescriptor(
					"ZER002",
					"No Consumers",
					"No consumers found to register",
					"Generation",
					DiagnosticSeverity.Info,
					true),
				Location.None));
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(
			new DiagnosticDescriptor(
				"ZER003",
				"Found Consumers",
				$"Found {consumers.Length} consumer(s) to register",
				"Generation",
				DiagnosticSeverity.Info,
				true),
			Location.None));

		// Retrieve the IConsumer<T> symbol from MassTransit
		var consumerInterfaceSymbol = compilation.GetTypeByMetadataName("MassTransit.IConsumer`1");

		if (consumerInterfaceSymbol == null)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				new DiagnosticDescriptor(
					"ZER004",
					"Missing Dependency",
					"MassTransit.IConsumer<T> interface not found. Ensure MassTransit is referenced.",
					"Generation",
					DiagnosticSeverity.Warning,
					true),
				Location.None));
			return;
		}

		// Collect all consumer information
		var consumerInfos = new List<ConsumerInfo>();

		foreach (var classSymbol in consumers)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				new DiagnosticDescriptor(
					"ZER005",
					"Processing Consumer",
					$"Processing consumer: {classSymbol.ToDisplayString()}",
					"Generation",
					DiagnosticSeverity.Info,
					true),
				Location.None));

			var attribute = classSymbol.GetAttributes()
				.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ConsumerAttributeName);

			if (attribute == null || attribute.ConstructorArguments.Length < 2)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					new DiagnosticDescriptor(
						"ZER006",
						"Invalid Consumer",
						$"No valid Consumer attribute found on {classSymbol.ToDisplayString()}",
						"Generation",
						DiagnosticSeverity.Warning,
						true),
					Location.None));
				continue;
			}

			var topicName = attribute.ConstructorArguments[0].Value as string;
			var subscriptionName = attribute.ConstructorArguments[1].Value as string;

			if (string.IsNullOrEmpty(topicName) || string.IsNullOrEmpty(subscriptionName))
			{
				context.ReportDiagnostic(Diagnostic.Create(
					new DiagnosticDescriptor(
						"ZER007",
						"Invalid Consumer Arguments",
						$"Invalid topic or subscription name on {classSymbol.ToDisplayString()}",
						"Generation",
						DiagnosticSeverity.Warning,
						true),
					Location.None));
				continue;
			}

			// Map Azure Service Bus terminology to RabbitMQ
			// Topic -> Exchange
			// Subscription -> Queue
			consumerInfos.Add(new ConsumerInfo
			{
				EventName = classSymbol.ToDisplayString(),
				ExchangeName = topicName,
				QueueName = subscriptionName
			});
		}

		if (consumerInfos.Count == 0)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				new DiagnosticDescriptor(
					"ZER008",
					"No Valid Consumers",
					"No valid consumers found to generate code for",
					"Generation",
					DiagnosticSeverity.Info,
					true),
				Location.None));
			return;
		}

		var sourceBuilder = new StringBuilder();
		AppendUsings(sourceBuilder);
		AppendNamespace(sourceBuilder);
		AppendClass(sourceBuilder, consumerInfos);

		var source = sourceBuilder.ToString();
		context.ReportDiagnostic(Diagnostic.Create(
			new DiagnosticDescriptor(
				"ZER009",
				"Generated Source",
				$"Generated source for {consumerInfos.Count} consumer(s)",
				"Generation",
				DiagnosticSeverity.Info,
				true),
			Location.None));

		context.AddSource($"{FileName}.g.cs", SourceText.From(source, Encoding.UTF8));
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
		sourceBuilder.AppendLine($"    public static void {MethodName}(this IRabbitMqBusFactoryConfigurator cfg, IBusRegistrationContext context)");
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
				cfg.ReceiveEndpoint("{{consumer.QueueName}}", e =>
				{
					e.Bind("{{consumer.ExchangeName}}");
					e.ConfigureConsumer<{{consumer.EventName}}>(context);
				});
			"""
		);
	}

	private class ConsumerInfo
	{
		public string EventName { get; set; } = string.Empty;
		public string ExchangeName { get; set; } = string.Empty;
		public string QueueName { get; set; } = string.Empty;
	}
} 