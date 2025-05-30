using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Zooper.Cheetah.Generators.RabbitMq;

[Generator]
public class ConsumerRegistrationGenerator : IIncrementalGenerator
{
	private const string FileName = "MassTransitConsumerRegistration";
	private const string BaseNamespace = "Zooper.Cheetah.Integrations.RabbitMq";
	private const string NamespaceSuffix = "Registry";
	private const string ClassName = "MassTransitConsumerRegistration";
	private const string MethodName = "RegisterConsumers";
	private const string ConsumerNameAttribute = "Zooper.Cheetah.Attributes.ConsumerAttribute";

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Register a syntax provider that filters for class declarations with attributes
		var consumerClasses = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				"Zooper.Cheetah.Attributes.ConsumerAttribute",
				(
					node,
					_) => node is ClassDeclarationSyntax,
				(
					syntaxContext,
					_) => (INamedTypeSymbol)syntaxContext.TargetSymbol
			);

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
	/// Executes the generation logic.
	/// </summary>
	private static void Execute(
		Compilation compilation,
		ImmutableArray<INamedTypeSymbol> consumers,
		SourceProductionContext context)
	{
		context.ReportDiagnostic(
			Diagnostic.Create(
				new DiagnosticDescriptor(
					"ZER001",
					"Generator Execution",
					"Executing ConsumerRegistrationGenerator",
					"Generation",
					DiagnosticSeverity.Info,
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
						"ZER002",
						"No Consumers",
						"No consumers found to register",
						"Generation",
						DiagnosticSeverity.Info,
						true
					),
					Location.None
				)
			);
			return;
		}

		context.ReportDiagnostic(
			Diagnostic.Create(
				new DiagnosticDescriptor(
					"ZER003",
					"Found Consumers",
					$"Found {consumers.Length} consumer(s) to register",
					"Generation",
					DiagnosticSeverity.Info,
					true
				),
				Location.None
			)
		);

		// Get project namespace
		string projectNamespace = GetFullNamespace();
		context.ReportDiagnostic(
			Diagnostic.Create(
				new DiagnosticDescriptor(
					"ZER008",
					"Project Namespace",
					$"Using namespace: {projectNamespace}",
					"Generation",
					DiagnosticSeverity.Info,
					true
				),
				Location.None
			)
		);

		// Retrieve the IConsumer<T> symbol from MassTransit
		var consumerInterfaceSymbol = compilation.GetTypeByMetadataName("MassTransit.IConsumer`1");

		if (consumerInterfaceSymbol == null)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					new DiagnosticDescriptor(
						"ZER004",
						"Missing Dependency",
						"MassTransit.IConsumer<T> interface not found. Ensure MassTransit is referenced.",
						"Generation",
						DiagnosticSeverity.Warning,
						true
					),
					Location.None
				)
			);
			return;
		}

		// Collect all consumer information
		var consumerInfos = new List<ConsumerInfo>();

		foreach (var classSymbol in consumers)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					new DiagnosticDescriptor(
						"ZER005",
						"Processing Consumer",
						$"Processing consumer: {classSymbol.ToDisplayString()}",
						"Generation",
						DiagnosticSeverity.Info,
						true
					),
					Location.None
				)
			);

			// Get the message type from IConsumer<T>
			var interfaceType = classSymbol.AllInterfaces.FirstOrDefault(
				i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, consumerInterfaceSymbol)
			);

			if (interfaceType == null || interfaceType.TypeArguments.Length != 1)
			{
				context.ReportDiagnostic(
					Diagnostic.Create(
						new DiagnosticDescriptor(
							"ZER006",
							"Invalid Consumer",
							$"Consumer {classSymbol.ToDisplayString()} does not implement IConsumer<T>",
							"Generation",
							DiagnosticSeverity.Warning,
							true
						),
						Location.None
					)
				);
				continue;
			}

			var messageType = interfaceType.TypeArguments[0];
			var messageTypeName = messageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

			consumerInfos.Add(
				new ConsumerInfo
				{
					ClassName = classSymbol.ToDisplayString(),
					InterfaceName = messageTypeName
				}
			);
		}

		// Generate the source code
		if (consumerInfos.Count > 0)
		{
			var source = GenerateSource(consumerInfos, projectNamespace);
			context.ReportDiagnostic(
				Diagnostic.Create(
					new DiagnosticDescriptor(
						"ZER007",
						"Generated Source",
						$"Generated source for {consumerInfos.Count} consumer(s)",
						"Generation",
						DiagnosticSeverity.Info,
						true
					),
					Location.None
				)
			);
			context.AddSource($"{FileName}.g.cs", SourceText.From(source, Encoding.UTF8));
		}
	}

	/// <summary>
	/// Gets the full namespace for the generated code
	/// </summary>
	private static string GetFullNamespace()
	{
		return $"{BaseNamespace}.{NamespaceSuffix}";
	}

	private static string GenerateSource(List<ConsumerInfo> consumerInfos, string projectNamespace)
	{
		var sb = new StringBuilder();
		sb.AppendLine("// <auto-generated/>");
		sb.AppendLine("using System;");
		sb.AppendLine("using System.Collections.Generic;");
		sb.AppendLine("using MassTransit;");
		sb.AppendLine();
		sb.AppendLine($"namespace {projectNamespace};");
		sb.AppendLine();
		sb.AppendLine($"public static class {ClassName}");
		sb.AppendLine("{");
		sb.AppendLine($"    public static void {MethodName}(this IBusRegistrationConfigurator configurator)");
		sb.AppendLine("    {");

		foreach (var info in consumerInfos)
		{
			sb.AppendLine($"        configurator.AddConsumer<{info.ClassName}>();");
		}

		sb.AppendLine("    }");
		sb.AppendLine("}");
		sb.AppendLine();

		return sb.ToString();
	}

	/// <summary>
	/// Represents information about a consumer.
	/// </summary>
	private class ConsumerInfo
	{
		public string ClassName { get; set; } = string.Empty;
		public string InterfaceName { get; set; } = string.Empty;
	}
}