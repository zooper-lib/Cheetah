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

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Register a syntax provider that filters for class declarations with attributes
		var consumerClasses = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: IsSyntaxTargetForGeneration, // Filter syntax nodes
				transform: GetSemanticTargetForGeneration // Transform to semantic symbols
			)
			.Where(static classSymbol => classSymbol != null)!; // Filter out nulls

		// Combine the compilation with the collected consumer symbols
		var compilationAndConsumers = context.CompilationProvider.Combine(consumerClasses.Collect());

		// Register the source output
		context.RegisterSourceOutput(
			compilationAndConsumers,
			(
				spc,
				source) => Execute(source.Left, source.Right, spc)
		);
	}

	/// <summary>
	/// Predicate to identify candidate classes with attributes.
	/// </summary>
	private static bool IsSyntaxTargetForGeneration(
		SyntaxNode node,
		CancellationToken cancellationToken)
	{
		return node is ClassDeclarationSyntax classDeclaration &&
		       classDeclaration.AttributeLists.Count > 0;
	}

	/// <summary>
	/// Transforms a syntax node into a semantic symbol if it has attributes.
	/// </summary>
	private static INamedTypeSymbol? GetSemanticTargetForGeneration(
		GeneratorSyntaxContext context,
		CancellationToken cancellationToken)
	{
		var classDeclaration = (ClassDeclarationSyntax)context.Node;
		var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) as INamedTypeSymbol;

		if (classSymbol == null)
			return null;

		// Check if the class has the ConsumerAttribute using fully qualified string name
		foreach (var attribute in classSymbol.GetAttributes())
		{
			if (attribute.AttributeClass == null)
				continue;

			// Use fully qualified string name to identify the attribute
			if (attribute.AttributeClass.ToDisplayString() == ConsumerAttributeName)
			{
				return classSymbol;
			}
		}

		return null;
	}

	/// <summary>
	/// Executes the generation logic.
	/// </summary>
	private static void Execute(
		Compilation compilation,
		ImmutableArray<INamedTypeSymbol?> consumers,
		SourceProductionContext context)
	{
		context.ReportDiagnostic(
			Diagnostic.Create(
				new DiagnosticDescriptor(
					"ZEA001",
					"Starting generation",
					"Starting generation process",
					"Generation",
					DiagnosticSeverity.Warning,
					true
				),
				Location.None
			)
		);

		if (consumers.IsDefaultOrEmpty)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					new DiagnosticDescriptor(
						"ZEA002",
						"No consumers",
						"No consumers found in compilation",
						"Generation",
						DiagnosticSeverity.Warning,
						true
					),
					Location.None
				)
			);
			return;
		}

		// Retrieve the ConsumerAttribute symbol using fully qualified string name
		var consumerAttributeSymbol = compilation.GetTypeByMetadataName(ConsumerAttributeName);

		if (consumerAttributeSymbol == null)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					new DiagnosticDescriptor(
						"ZEA003",
						"Attribute not found",
						$"Could not find attribute {ConsumerAttributeName}",
						"Generation",
						DiagnosticSeverity.Error,
						true
					),
					Location.None
				)
			);
			return;
		}

		// Retrieve the IConsumer<T> symbol from MassTransit
		var consumerInterfaceSymbol = compilation.GetTypeByMetadataName("MassTransit.IConsumer`1");

		if (consumerInterfaceSymbol == null)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					new DiagnosticDescriptor(
						"ZEA004",
						"IConsumer not found",
						"Could not find MassTransit.IConsumer`1 interface",
						"Generation",
						DiagnosticSeverity.Error,
						true
					),
					Location.None
				)
			);
			return;
		}

		// Collect all consumer information
		var consumerInfos = new List<ConsumerInfo>();

		foreach (var classSymbol in consumers.Distinct())
		{
			if (classSymbol == null)
				continue;

			context.ReportDiagnostic(
				Diagnostic.Create(
					new DiagnosticDescriptor(
						"ZEA005",
						"Processing consumer",
						$"Processing consumer: {classSymbol.ToDisplayString()}",
						"Generation",
						DiagnosticSeverity.Info,
						true
					),
					Location.None
				)
			);

			// Retrieve the ConsumerAttribute data using symbol comparison
			var attributeData = classSymbol.GetAttributes()
				.FirstOrDefault(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, consumerAttributeSymbol));

			if (attributeData == null)
			{
				context.ReportDiagnostic(
					Diagnostic.Create(
						new DiagnosticDescriptor(
							"ZEA006",
							"No attribute",
							$"No ConsumerAttribute found on {classSymbol.ToDisplayString()}",
							"Generation",
							DiagnosticSeverity.Warning,
							true
						),
						Location.None
					)
				);
				continue;
			}

			// Extract attribute arguments
			var entityName = attributeData.ConstructorArguments.Length > 0 ? attributeData.ConstructorArguments[0].Value as string : null;
			var subscriptionName = attributeData.ConstructorArguments.Length > 1
				? attributeData.ConstructorArguments[1].Value as string
				: null;

			if (entityName is null || subscriptionName is null)
			{
				context.ReportDiagnostic(
					Diagnostic.Create(
						new DiagnosticDescriptor(
							"ZEA007",
							"Invalid attribute arguments",
							$"Invalid attribute arguments on {classSymbol.ToDisplayString()}",
							"Generation",
							DiagnosticSeverity.Warning,
							true
						),
						Location.None
					)
				);
				continue;
			}

			consumerInfos.Add(
				new ConsumerInfo
				{
					EventName = classSymbol.ToDisplayString(),
					ChannelName = entityName,
					SubscriptionName = subscriptionName
				}
			);

			context.ReportDiagnostic(
				Diagnostic.Create(
					new DiagnosticDescriptor(
						"ZEA008",
						"Added consumer",
						$"Added consumer {classSymbol.ToDisplayString()} with channel {entityName} and subscription {subscriptionName}",
						"Generation",
						DiagnosticSeverity.Info,
						true
					),
					Location.None
				)
			);
		}

		if (consumerInfos.Count == 0)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					new DiagnosticDescriptor(
						"ZEA009",
						"No valid consumers",
						"No valid consumers found",
						"Generation",
						DiagnosticSeverity.Warning,
						true
					),
					Location.None
				)
			);
			return;
		}

		// Generate the registration code
		var sourceBuilder = new StringBuilder();

		AppendUsings(sourceBuilder);
		AppendNamespace(sourceBuilder);
		AppendClass(sourceBuilder, consumerInfos);

		var source = sourceBuilder.ToString();

		context.ReportDiagnostic(
			Diagnostic.Create(
				new DiagnosticDescriptor(
					"ZEA010",
					"Generated source",
					$"Generated source code:\n{source}",
					"Generation",
					DiagnosticSeverity.Info,
					true
				),
				Location.None
			)
		);

		// Add the generated source to the compilation
		context.AddSource($"{FileName}.g.cs", SourceText.From(source, Encoding.UTF8));

		context.ReportDiagnostic(
			Diagnostic.Create(
				new DiagnosticDescriptor(
					"ZEA011",
					"Source added",
					$"Added source file {FileName}.g.cs",
					"Generation",
					DiagnosticSeverity.Info,
					true
				),
				Location.None
			)
		);
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

	private static void AppendClass(
		StringBuilder sourceBuilder,
		List<ConsumerInfo> consumers)
	{
		sourceBuilder.AppendLine($"public static class {ClassName}");
		sourceBuilder.AppendLine("{");

		AppendMethod(sourceBuilder, consumers);

		sourceBuilder.AppendLine("}");
	}

	private static void AppendMethod(
		StringBuilder sourceBuilder,
		List<ConsumerInfo> consumers)
	{
		sourceBuilder.AppendLine(
			$"    public static void {MethodName}(this IServiceBusBusFactoryConfigurator cfg, IBusRegistrationContext context)"
		);
		sourceBuilder.AppendLine("    {");

		AppendAllConsumers(sourceBuilder, consumers);

		sourceBuilder.AppendLine("    }");
	}

	private static void AppendAllConsumers(
		StringBuilder sourceBuilder,
		List<ConsumerInfo> consumers)
	{
		foreach (var consumer in consumers)
		{
			AppendConsumer(sourceBuilder, consumer);
		}
	}

	private static void AppendConsumer(
		StringBuilder sourceBuilder,
		ConsumerInfo consumer)
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

	/// <summary>
	/// Represents information about a consumer.
	/// </summary>
	private class ConsumerInfo
	{
		public string EventName { get; set; } = string.Empty;
		public string ChannelName { get; set; } = string.Empty;
		public string SubscriptionName { get; set; } = string.Empty;
	}
}