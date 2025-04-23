using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Zooper.Cheetah.Generators.AzureServiceBus;

[Generator]
public sealed class EndpointGenerator : IIncrementalGenerator
{
	private const string FileName = "MassTransitEndpointConfiguration";
	private const string BaseNamespace = "Zooper.Cheetah.Integrations.AzureServiceBus";
	private const string NamespaceSuffix = "Endpoints";
	private const string ClassName = "MassTransitEndpointConfiguration";
	private const string MethodName = "ConfigureEndpoints";
	private const string EntityNameAttributeName = "MassTransit.EntityNameAttribute";
	private const string ExcludeFromTopologyAttributeName = "MassTransit.ExcludeFromTopologyAttribute";
	private const string ChannelAttributeName = "Zooper.Cheetah.Attributes.ChannelAttribute";
	private const string DefaultServiceName = "account-service"; // Default service name
	private const string ConsumerAttributeName = "Zooper.Cheetah.Attributes.ConsumerAttribute";

	// Diagnostic descriptors for logging
	private static readonly DiagnosticDescriptor ExecutionInfo = new(
		"ZEE001",
		"Generator Execution",
		"{0}",
		"Generation",
		DiagnosticSeverity.Info,
		true);

	private static readonly DiagnosticDescriptor DebugInfo = new(
		"ZEE002",
		"Generator Debug",
		"{0}",
		"Debug",
		DiagnosticSeverity.Info,
		true);

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Find consumer implementations in the assembly
		var consumerImplementations = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				ConsumerAttributeName,
				(node, _) => node is ClassDeclarationSyntax,
				(syntaxContext, ct) =>
				{
					ct.ThrowIfCancellationRequested();
					var symbol = (INamedTypeSymbol)syntaxContext.TargetSymbol;

					// Find IConsumer<T> interface
					foreach (var intf in symbol.AllInterfaces)
					{
						if (intf.OriginalDefinition.ToDisplayString() == "MassTransit.IConsumer<T>")
						{
							if (intf.TypeArguments.Length == 1)
							{
								var messageType = intf.TypeArguments[0];
								return new ConsumerInfo
								{
									ConsumerType = symbol.ToDisplayString(),
									MessageType = messageType.ToDisplayString()
								};
							}
						}
					}
					return null;
				})
			.Where(info => info != null);

		// Find all interface declarations that might be integration events
		var eventInterfaces = context.SyntaxProvider
			.CreateSyntaxProvider<InterfaceInfo>(
				predicate: (node, _) =>
				{
					// Be more selective in predicate to improve performance
					if (node is InterfaceDeclarationSyntax intf)
					{
						// Quick check for potential event interfaces
						var hasIntegrationInName = intf.Identifier.ToString().Contains("Integration");
						var hasIntegrationInBase = intf.BaseList?.Types.Any(t => t.ToString().Contains("Integration")) == true;
						return hasIntegrationInName || hasIntegrationInBase;
					}
					return false;
				},
				transform: (ctx, ct) =>
				{
					ct.ThrowIfCancellationRequested();
					var interfaceNode = (InterfaceDeclarationSyntax)ctx.Node;
					var symbol = ctx.SemanticModel.GetDeclaredSymbol(interfaceNode) as INamedTypeSymbol;
					if (symbol == null) return new InterfaceInfo { DebugInfo = "Symbol was null" };

					var info = new InterfaceInfo
					{
						InterfaceName = symbol.ToDisplayString(),
						DebugInfo = $"Interface: {symbol.ToDisplayString()}, Base Interfaces: {string.Join(", ", symbol.Interfaces.Select(i => i.ToDisplayString()))}"
					};

					// Is this an integration event interface?
					if (!info.InterfaceName.Contains("IIntegrationEvent"))
					{
						info.DebugInfo += $" | Not an IIntegrationEvent interface";
						return info;
					}

					// Log members
					var memberList = new StringBuilder();
					foreach (var member in symbol.GetMembers())
					{
						memberList.AppendLine($"Member: {member.Name}, Kind: {member.Kind}");

						if (member is INamedTypeSymbol nestedType)
						{
							foreach (var attr in nestedType.GetAttributes())
							{
								memberList.AppendLine($"  Attribute: {attr.AttributeClass?.ToDisplayString()}");
							}
						}
					}
					info.DebugInfo += $" | Members: {memberList}";

					// Look for nested records/classes with EntityName attribute
					foreach (var member in symbol.GetMembers())
					{
						if (member is INamedTypeSymbol nestedType &&
							(nestedType.TypeKind == TypeKind.Struct || nestedType.TypeKind == TypeKind.Class))
						{
							foreach (var attr in nestedType.GetAttributes())
							{
								if (attr.AttributeClass?.ToDisplayString() == EntityNameAttributeName)
								{
									if (attr.ConstructorArguments.Length > 0)
									{
										var entityName = attr.ConstructorArguments[0].Value as string;
										if (!string.IsNullOrEmpty(entityName))
										{
											info.Members.Add(new EventMemberInfo
											{
												MemberName = nestedType.Name,
												FullName = nestedType.ToDisplayString(),
												EventName = entityName
											});
										}
									}
								}
							}
						}
					}
					return info;
				})
			.Where(info => info.InterfaceName != null && info.Members.Any());

		// Find all standalone classes/records with Channel attribute
		var channelEvents = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				ChannelAttributeName,
				(node, _) =>
				{
					// Add more diagnostics
					if (node is ClassDeclarationSyntax classDecl)
					{
						foreach (var attrList in classDecl.AttributeLists)
						{
							foreach (var attrNode in attrList.Attributes)
							{
								var attrName = attrNode.Name.ToString();
								if (attrName == "Channel" || attrName == "ChannelAttribute")
								{
									return true;
								}
							}
						}
					}
					return node is ClassDeclarationSyntax or RecordDeclarationSyntax;
				},
				(context, ct) =>
				{
					ct.ThrowIfCancellationRequested();
					var symbol = (INamedTypeSymbol)context.TargetSymbol;

					// Log all attributes on this symbol for debugging
					var allAttributes = new StringBuilder();
					foreach (var attribute in symbol.GetAttributes())
					{
						allAttributes.AppendLine($"Found attribute: {attribute.AttributeClass?.ToDisplayString()}");
					}

					var attr = symbol.GetAttributes()
						.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ChannelAttributeName);

					if (attr != null && attr.ConstructorArguments.Length > 0)
					{
						var channelName = attr.ConstructorArguments[0].Value as string;

						// Also look for EntityName attribute for a more specific name
						string entityName = null;
						var entityNameAttr = symbol.GetAttributes()
							.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == EntityNameAttributeName);
						if (entityNameAttr != null && entityNameAttr.ConstructorArguments.Length > 0)
						{
							entityName = entityNameAttr.ConstructorArguments[0].Value as string;
						}

						return new StandaloneEventInfo
						{
							TypeName = symbol.ToDisplayString(),
							ChannelName = channelName ?? "",
							EventName = entityName ?? channelName ?? symbol.Name.ToLowerInvariant(),
							DebugInfo = $"All attributes: {allAttributes}"
						};
					}
					return null;
				})
			.Where(info => info != null);

		// Register a syntax provider for compilation debugging
		var assembliesInfo = context.CompilationProvider.Select((compilation, ct) =>
		{
			ct.ThrowIfCancellationRequested();

			var referencedAssemblies = new List<string>();
			foreach (var reference in compilation.References)
			{
				try
				{
					if (reference.Display != null)
					{
						referencedAssemblies.Add(reference.Display);
					}
				}
				catch { /* Ignore exceptions during diagnostic gathering */ }
			}

			return (
				AssemblyName: compilation.AssemblyName,
				ReferencedAssemblies: referencedAssemblies
			);
		});

		// Combine the consumer implementations with the event interfaces and channel events
		var compilationAndInfo = context.CompilationProvider
			.Combine(consumerImplementations.Collect())
			.Combine(eventInterfaces.Collect())
			.Combine(channelEvents.Collect())
			.Combine(assembliesInfo);

		// Register the source output
		context.RegisterSourceOutput(
			compilationAndInfo,
			(spc, source) => Execute(
				source.Left.Left.Left.Left,
				source.Left.Left.Left.Right,
				source.Left.Left.Right,
				source.Left.Right,
				source.Right,
				spc)
		);
	}

	private static void Execute(
		Compilation compilation,
		ImmutableArray<ConsumerInfo?> consumers,
		ImmutableArray<InterfaceInfo> eventInterfaces,
		ImmutableArray<StandaloneEventInfo> channelEvents,
		(string AssemblyName, List<string> ReferencedAssemblies) assemblyInfo,
		SourceProductionContext context)
	{
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, "=== EXECUTING ENDPOINT GENERATOR ==="));
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Compilation: {compilation.AssemblyName}"));

		// Report diagnostic information about the assembly and its references
		context.ReportDiagnostic(Diagnostic.Create(DebugInfo, Location.None, $"Assembly: {assemblyInfo.AssemblyName}"));
		context.ReportDiagnostic(Diagnostic.Create(DebugInfo, Location.None, $"Referenced Assemblies Count: {assemblyInfo.ReferencedAssemblies.Count}"));

		foreach (var referencedAssembly in assemblyInfo.ReferencedAssemblies.Where(a => a.Contains("MassTransit") || a.Contains("Zooper")))
		{
			context.ReportDiagnostic(Diagnostic.Create(DebugInfo, Location.None, $"Referenced: {referencedAssembly}"));
		}

		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Found {consumers.Length} consumer(s)"));
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Found {eventInterfaces.Length} event interface(s)"));
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Found {channelEvents.Length} channel-attributed events"));

		// Detailed debug for consumers
		foreach (var consumer in consumers)
		{
			context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None,
				$"Consumer: {consumer?.ConsumerType} for message {consumer?.MessageType}"));

			if (consumer != null)
			{
				context.ReportDiagnostic(Diagnostic.Create(DebugInfo, Location.None,
					$"Consumer debug: {consumer.DebugInfo}"));
			}
		}

		// Detailed debug for interfaces
		foreach (var eventInterface in eventInterfaces)
		{
			context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None,
				$"Event interface: {eventInterface.InterfaceName} with {eventInterface.Members.Count} members"));

			context.ReportDiagnostic(Diagnostic.Create(DebugInfo, Location.None,
				$"Interface debug: {eventInterface.DebugInfo}"));

			foreach (var member in eventInterface.Members)
			{
				context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None,
					$"  Member: {member.MemberName} - {member.EventName}"));
			}
		}

		// Detailed debug for channel events
		foreach (var channelEvent in channelEvents)
		{
			context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None,
				$"Channel event: {channelEvent.TypeName} with channel {channelEvent.ChannelName} and event name {channelEvent.EventName}"));

			context.ReportDiagnostic(Diagnostic.Create(DebugInfo, Location.None,
				$"Channel event debug: {channelEvent.DebugInfo}"));
		}

		// Get project namespace
		string projectNamespace = GetFullNamespace();
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Using namespace: {projectNamespace}"));

		var endpointInfos = new List<EndpointInfo>();

		// If we don't have any consumers or any event types (both interfaces and channel events), generate minimal implementation
		if (consumers.IsDefaultOrEmpty || (eventInterfaces.IsDefaultOrEmpty && channelEvents.IsDefaultOrEmpty))
		{
			context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None,
				"No consumers or event types found. Generating minimal implementation."));
			GenerateMinimalImplementation(context);
			return;
		}

		// Map consumers to event interfaces and members
		foreach (var consumer in consumers.Where(c => c != null))
		{
			bool matched = false;
			context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Processing consumer: {consumer!.ConsumerType} for message: {consumer.MessageType}"));

			var messageParts = consumer.MessageType.Split('.');
			var messageTypeName = messageParts.Length > 0 ? messageParts[messageParts.Length - 1] : "";

			context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Message type name: {messageTypeName}"));

			// First, check if the consumer has a Consumer attribute with both endpoint and subscription
			if (!string.IsNullOrWhiteSpace(consumer.Endpoint))
			{
				endpointInfos.Add(new EndpointInfo
				{
					EventName = consumer.Endpoint,
					ConsumerType = consumer.ConsumerType,
					MessageType = consumer.MessageType,
					SubscriptionName = consumer.Subscription ?? $"{DefaultServiceName}-subscription"
				});

				context.ReportDiagnostic(Diagnostic.Create(
					ExecutionInfo,
					Location.None,
					$"Mapped consumer {consumer.ConsumerType} to endpoint {consumer.Endpoint} using Consumer attribute"));

				matched = true;
			}

			// If not matched with Consumer attribute, try nested interface events
			if (!matched)
			{
				// Try to match with nested interface events first
				foreach (var eventInterface in eventInterfaces)
				{
					foreach (var member in eventInterface.Members)
					{
						context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None,
							$"Checking {consumer.MessageType} ends with {member.MemberName} or contains {member.FullName}"));

						if (consumer.MessageType.EndsWith("." + member.MemberName) ||
							consumer.MessageType.Contains("." + member.FullName))
						{
							endpointInfos.Add(new EndpointInfo
							{
								EventName = member.EventName,
								ConsumerType = consumer.ConsumerType,
								MessageType = consumer.MessageType,
								SubscriptionName = $"{DefaultServiceName}-subscription"
							});

							context.ReportDiagnostic(Diagnostic.Create(
								ExecutionInfo,
								Location.None,
								$"Mapped consumer {consumer.ConsumerType} to interface event {member.EventName}"));

							matched = true;
							break;
						}
					}
					if (matched) break;
				}
			}

			// If still not matched, try to infer from the message type name as a last resort
			if (!matched)
			{
				context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None,
					$"No explicit match found for {consumer.MessageType}, trying to infer from message name"));

				// Extract simple name from message type and use as event name
				var eventName = messageTypeName.ToLowerInvariant();

				// Clean up common suffixes for better naming
				if (eventName.EndsWith("event"))
					eventName = eventName.Substring(0, eventName.Length - 5);
				if (eventName.EndsWith("message"))
					eventName = eventName.Substring(0, eventName.Length - 7);

				// Add dashes between words in PascalCase
				eventName = Regex.Replace(eventName, "([a-z])([A-Z])", "$1-$2").ToLowerInvariant();

				endpointInfos.Add(new EndpointInfo
				{
					EventName = eventName,
					ConsumerType = consumer.ConsumerType,
					MessageType = consumer.MessageType,
					SubscriptionName = $"{DefaultServiceName}-subscription"
				});

				context.ReportDiagnostic(Diagnostic.Create(
					ExecutionInfo,
					Location.None,
					$"Inferred event name {eventName} for consumer {consumer.ConsumerType}"));
			}
		}

		if (endpointInfos.Count == 0)
		{
			// Generate minimal implementation
			context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None,
				"No endpoints could be mapped. Generating minimal implementation."));
			GenerateMinimalImplementation(context);
			return;
		}

		var sourceBuilder = new StringBuilder();
		AppendUsings(sourceBuilder);
		AppendNamespace(sourceBuilder, projectNamespace);
		AppendClass(sourceBuilder, endpointInfos);

		var source = sourceBuilder.ToString();
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Generated source code with {endpointInfos.Count} endpoint(s)"));

		context.AddSource($"{FileName}.g.cs", SourceText.From(source, Encoding.UTF8));
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Added source file: {FileName}.g.cs"));
	}

	private static void GenerateMinimalImplementation(SourceProductionContext context)
	{
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, "Generating minimal implementation"));

		var sourceBuilder = new StringBuilder();
		AppendUsings(sourceBuilder);
		AppendNamespace(sourceBuilder, GetFullNamespace());

		// Create a minimal class with the extension method
		sourceBuilder.AppendLine($"public static class {ClassName}");
		sourceBuilder.AppendLine("{");
		sourceBuilder.AppendLine($"    public static void {MethodName}(this IServiceBusBusFactoryConfigurator cfg, IBusRegistrationContext context, string serviceName = \"{DefaultServiceName}\")");
		sourceBuilder.AppendLine("    {");
		sourceBuilder.AppendLine("        // This is a minimal implementation generated because no consumers were found");
		sourceBuilder.AppendLine("        // When you add consumers that implement IConsumer<YourMessage> and message interfaces");
		sourceBuilder.AppendLine("        // with [EntityName] attributes, this method will be regenerated with proper endpoints");
		sourceBuilder.AppendLine("    }");
		sourceBuilder.AppendLine("}");

		var source = sourceBuilder.ToString();
		context.AddSource($"{FileName}.g.cs", SourceText.From(source, Encoding.UTF8));
		context.ReportDiagnostic(Diagnostic.Create(ExecutionInfo, Location.None, $"Added minimal source file: {FileName}.g.cs"));
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
		sourceBuilder.AppendLine("using MassTransit;");
		sourceBuilder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
		sourceBuilder.AppendLine();
	}

	private static void AppendNamespace(StringBuilder sourceBuilder, string projectNamespace)
	{
		sourceBuilder.AppendLine($"namespace {projectNamespace};");
		sourceBuilder.AppendLine();
	}

	private static void AppendClass(StringBuilder sourceBuilder, List<EndpointInfo> endpoints)
	{
		sourceBuilder.AppendLine($"public static class {ClassName}");
		sourceBuilder.AppendLine("{");
		AppendMethod(sourceBuilder, endpoints);
		sourceBuilder.AppendLine("}");
	}

	private static void AppendMethod(StringBuilder sourceBuilder, List<EndpointInfo> endpoints)
	{
		sourceBuilder.AppendLine($"    public static void {MethodName}(this IServiceBusBusFactoryConfigurator cfg, IBusRegistrationContext context, string serviceName = \"{DefaultServiceName}\")");
		sourceBuilder.AppendLine("    {");
		AppendAllEndpoints(sourceBuilder, endpoints);
		sourceBuilder.AppendLine("    }");
	}

	private static void AppendAllEndpoints(StringBuilder sourceBuilder, List<EndpointInfo> endpoints)
	{
		foreach (var endpoint in endpoints)
		{
			AppendEndpoint(sourceBuilder, endpoint);
		}
	}

	private static void AppendEndpoint(StringBuilder sourceBuilder, EndpointInfo endpoint)
	{
		sourceBuilder.AppendLine(
			$$"""
                cfg.ReceiveEndpoint(
                            $"{serviceName}-{{endpoint.EventName}}",
                            e =>
                            {
                                // Disable automatic subscriptions if you want full control
                                e.ConfigureConsumeTopology = false;

                                // Configure consumer at this endpoint
                                e.ConfigureConsumer<{{endpoint.ConsumerType}}>(context);

                                // Explicitly subscribe with a custom subscription name
                                // Using the same subscription name across all instances
                                e.Subscribe<{{endpoint.MessageType}}>($"{{endpoint.SubscriptionName}}");
                            }
                        );
            """
		);
	}

	private class ConsumerInfo
	{
		public string ConsumerType { get; set; } = string.Empty;
		public string MessageType { get; set; } = string.Empty;
		public string Endpoint { get; set; } = string.Empty;
		public string Subscription { get; set; } = string.Empty;
		public string DebugInfo { get; set; } = string.Empty;
	}

	private class InterfaceInfo
	{
		public string? InterfaceName { get; set; }
		public List<EventMemberInfo> Members { get; set; } = new List<EventMemberInfo>();
		public string DebugInfo { get; set; } = string.Empty;
	}

	private class EventMemberInfo
	{
		public string MemberName { get; set; } = string.Empty;
		public string FullName { get; set; } = string.Empty;
		public string EventName { get; set; } = string.Empty;
	}

	private class StandaloneEventInfo
	{
		public string TypeName { get; set; } = string.Empty;
		public string ChannelName { get; set; } = string.Empty;
		public string EventName { get; set; } = string.Empty;
		public string DebugInfo { get; set; } = string.Empty;
	}

	private class EndpointInfo
	{
		public string EventName { get; set; } = string.Empty;
		public string ConsumerType { get; set; } = string.Empty;
		public string MessageType { get; set; } = string.Empty;
		public string SubscriptionName { get; set; } = string.Empty;
	}
}