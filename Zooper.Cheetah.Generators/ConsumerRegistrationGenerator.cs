using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Zooper.Cheetah.Generators;

[Generator]
public class ConsumerRegistrationGenerator : IIncrementalGenerator
{
	private const string ConsumerNameAttribute = "Zooper.Cheetah.Attributes.ConsumerAttribute";

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Register a syntax provider that filters for class declarations with ConsumerAttribute
		var consumerClasses = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: IsCandidateClass, // Filter syntax nodes
				transform: GetSemanticTarget // Transform to semantic symbols
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
	/// Predicate to identify candidate classes with ConsumerAttribute.
	/// </summary>
	private static bool IsCandidateClass(
		SyntaxNode node,
		CancellationToken cancellationToken)
	{
		return node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };
	}

	/// <summary>
	/// Transforms a syntax node into a semantic symbol if it has ConsumerAttribute.
	/// </summary>
	private static INamedTypeSymbol? GetSemanticTarget(
		GeneratorSyntaxContext context,
		CancellationToken cancellationToken)
	{
		var classDeclaration = (ClassDeclarationSyntax)context.Node;
		var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) as INamedTypeSymbol;

		if (classSymbol == null)
			return null;

		var consumerAttributeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(ConsumerNameAttribute);
		if (consumerAttributeSymbol == null)
			return null;

		// Check if the class has the ConsumerAttribute using symbol comparison
		foreach (var attribute in classSymbol.GetAttributes())
		{
			if (attribute.AttributeClass == null)
				continue;

			if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, consumerAttributeSymbol))
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
		if (consumers.IsDefaultOrEmpty)
			return;

		// Retrieve the ConsumerAttribute symbol using fully qualified string name
		var consumerAttributeSymbol = compilation.GetTypeByMetadataName(ConsumerNameAttribute);

		if (consumerAttributeSymbol == null)
		{
			// Attribute not found; nothing to generate
			return;
		}

		// Retrieve the IConsumer<T> symbol from MassTransit
		var consumerInterfaceSymbol = compilation.GetTypeByMetadataName("MassTransit.IConsumer`1");

		if (consumerInterfaceSymbol == null)
		{
			// IConsumer<T> interface not found; ensure MassTransit is referenced
			return;
		}

		// Collect all consumer information
		var consumerInfos = new List<ConsumerInfo>();

		foreach (var classSymbol in consumers.Distinct())
		{
			if (classSymbol == null)
				continue;

			// Retrieve the ConsumerAttribute data using symbol comparison
			var attributeData = classSymbol.GetAttributes()
				.FirstOrDefault(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, consumerAttributeSymbol));

			if (attributeData == null)
				continue;

			// Extract attribute arguments
			var entityName = attributeData.ConstructorArguments.Length > 0 ? attributeData.ConstructorArguments[0].Value as string : null;
			var subscriptionName = attributeData.ConstructorArguments.Length > 1
				? attributeData.ConstructorArguments[1].Value as string
				: null;

			if (entityName is null || subscriptionName is null)
				continue;

			// Get the message type from IConsumer<T>
			var interfaceType = classSymbol.AllInterfaces.FirstOrDefault(
				i =>
					SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, consumerInterfaceSymbol)
			);

			if (interfaceType == null || interfaceType.TypeArguments.Length != 1)
				continue;

			var messageType = interfaceType.TypeArguments[0];
			var messageTypeName = messageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

			consumerInfos.Add(
				new ConsumerInfo
				{
					ClassName = classSymbol.ToDisplayString(),
					InterfaceName = messageTypeName,
					EntityName = entityName,
					SubscriptionName = subscriptionName
				}
			);
		}

		// Generate the source code
		if (consumerInfos.Count > 0)
		{
			var source = GenerateSource(consumerInfos);
			context.AddSource("ConsumerRegistration.g.cs", SourceText.From(source, Encoding.UTF8));
		}
	}

	private static string GenerateSource(List<ConsumerInfo> consumerInfos)
	{
		var sb = new StringBuilder();
		sb.AppendLine("// <auto-generated/>");
		sb.AppendLine("using System;");
		sb.AppendLine("using System.Collections.Generic;");
		sb.AppendLine("using MassTransit;");
		sb.AppendLine();
		sb.AppendLine("namespace Zooper.Cheetah.Generators;");
		sb.AppendLine();
		sb.AppendLine("public static class ConsumerRegistration");
		sb.AppendLine("{");
		sb.AppendLine("    public static void RegisterConsumers(IBusRegistrationConfigurator configurator)");
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
		public string EntityName { get; set; } = string.Empty;
		public string SubscriptionName { get; set; } = string.Empty;
	}
}