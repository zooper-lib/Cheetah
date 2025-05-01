using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Zooper.Cheetah.Generators.AzureServiceBus;

[Generator]
public class ReceiveEndpointSourceGenerator : IIncrementalGenerator
{
    private const string FileName = "MassTransitExtensions";
    private const string MethodName = "AddIntegrationEndpoints";
    private const string MassTransitNamespace = "MassTransit";
    private const string EntityNameAttributeMetadataName = "MassTransit.EntityNameAttribute";
    private const string ConsumerInterfaceMetadataName = "MassTransit.IConsumer`1";
    private const string GeneratedNamespace = "Zooper.Cheetah.AzureServiceBus.Generated";
    private const string EndpointSeparator = "-";
    private const string SubscriptionSuffix = "-subscription";
    private const string FileExtension = ".g.cs";
    private const string ConfiguratorParamName = "e";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Gather all types (including nested types and referenced assemblies) annotated with EntityNameAttribute
        var eventProvider = context.CompilationProvider.Select((comp, cancellationToken) =>
        {
            var events = new List<(INamedTypeSymbol Symbol, string Name)>();
            var entityAttr = comp.GetTypeByMetadataName(EntityNameAttributeMetadataName);
            if (entityAttr == null)
                return events;

            void ProcessType(INamedTypeSymbol type)
            {
                var attr = type.GetAttributes().FirstOrDefault(a =>
                    SymbolEqualityComparer.Default.Equals(a.AttributeClass, entityAttr));
                if (attr != null && attr.ConstructorArguments.Length > 0)
                {
                    var nameArg = attr.ConstructorArguments[0].Value as string;
                    if (!string.IsNullOrWhiteSpace(nameArg))
                        events.Add((type, nameArg));
                }
                // Recurse into nested types
                foreach (var nested in type.GetTypeMembers())
                    ProcessType(nested);
            }

            void CollectNamespace(INamespaceSymbol ns)
            {
                foreach (var type in ns.GetTypeMembers())
                    ProcessType(type);
                foreach (var child in ns.GetNamespaceMembers())
                    CollectNamespace(child);
            }

            CollectNamespace(comp.GlobalNamespace);
            return events;
        });

        // 2. Gather all consumers implementing MassTransit.IConsumer<TEvent>
        var consumerProvider = context.CompilationProvider.Select((comp, cancellationToken) =>
        {
            var consumers = new List<(INamedTypeSymbol Consumer, INamedTypeSymbol Event)>();
            var consumerIface = comp.GetTypeByMetadataName(ConsumerInterfaceMetadataName);
            if (consumerIface == null)
                return consumers;

            foreach (var tree in comp.SyntaxTrees)
            {
                var model = comp.GetSemanticModel(tree);
                foreach (var cls in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    var sym = model.GetDeclaredSymbol(cls) as INamedTypeSymbol;
                    if (sym == null) continue;

                    foreach (var iface in sym.AllInterfaces)
                    {
                        if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, consumerIface) &&
                            iface.TypeArguments.Length == 1 && iface.TypeArguments[0] is INamedTypeSymbol ev)
                        {
                            consumers.Add((sym, ev));
                            break;
                        }
                    }
                }
            }
            return consumers;
        });

        // 3. Combine event and consumer lists
        var combined = eventProvider.Combine(consumerProvider);

        // 4. Generate the extension method source
        context.RegisterSourceOutput(
            combined,
            (spc, data) =>
            {
                var events = data.Left.Distinct(new SymbolListComparer()).ToList();
                var consumers = data.Right;
                if (!events.Any()) return;

                // Collect namespaces for using directives
                var nsSet = new HashSet<string> { MassTransitNamespace };
                foreach (var (evtSym, _) in events)
                {
                    var ns = evtSym.ContainingNamespace;
                    if (!ns.IsGlobalNamespace)
                        nsSet.Add(ns.ToDisplayString());
                }
                foreach (var (consumerSym, _) in consumers)
                {
                    var ns = consumerSym.ContainingNamespace;
                    if (!ns.IsGlobalNamespace)
                        nsSet.Add(ns.ToDisplayString());
                }

                var sb = new StringBuilder();
                foreach (var ns in nsSet)
                    sb.AppendLine($"using {ns};");

                sb.AppendLine();
                sb.AppendLine($"namespace {GeneratedNamespace};");
                sb.AppendLine();
                sb.AppendLine($"public static class {FileName}");
                sb.AppendLine("{");
                sb.AppendLine(
                    $"    public static void {MethodName}(this IServiceBusBusFactoryConfigurator cfg, IRegistrationContext context, string serviceName, bool enableDeadLettering = true, bool publishFaults = false)"
                );
                sb.AppendLine("    {");

                // Generate unique counter for each consumer-event pair
                int consumerCounter = 0;

                foreach (var (evtSym, entityName) in events)
                {
                    var matched = consumers.Where(c => SymbolEqualityComparer.Default.Equals(c.Event, evtSym));
                    foreach (var (consumerSym, _) in matched)
                    {
                        consumerCounter++;
                        string topicVarName = $"topicName{consumerCounter}";
                        string subscriptionVarName = $"subscriptionName{consumerCounter}";

                        sb.AppendLine($"        var {topicVarName} = \"{entityName}\";");
                        sb.AppendLine($"        var {subscriptionVarName} = serviceName + \"{SubscriptionSuffix}\";");
                        sb.AppendLine();
                        
                        sb.AppendLine($"        cfg.SubscriptionEndpoint(");
                        sb.AppendLine($"            {subscriptionVarName},");
                        sb.AppendLine($"            {topicVarName},");
                        sb.AppendLine($"            {ConfiguratorParamName} =>");
                        sb.AppendLine("            {");
                        
                        sb.AppendLine($"                {ConfiguratorParamName}.PublishFaults = publishFaults;");
                        sb.AppendLine();
                        
                        if (true) // keeping conditional for future use
                        {
                            sb.AppendLine($"                {ConfiguratorParamName}.ConfigureDeadLetterQueueDeadLetterTransport();");
                            sb.AppendLine($"                {ConfiguratorParamName}.ConfigureDeadLetterQueueErrorTransport();");
                            sb.AppendLine();
                        }
                        
                        sb.AppendLine($"                {ConfiguratorParamName}.Consumer<{consumerSym.ToDisplayString()}>(context);");
                        sb.AppendLine("            });");
                        sb.AppendLine();
                    }
                }

                sb.AppendLine("    }");
                sb.AppendLine("}");

                spc.AddSource($"{FileName}{FileExtension}", SourceText.From(sb.ToString(), Encoding.UTF8));
            }
        );
    }

    private class SymbolListComparer : IEqualityComparer<(INamedTypeSymbol Symbol, string Name)>
    {
        public bool Equals((INamedTypeSymbol Symbol, string Name) x, (INamedTypeSymbol Symbol, string Name) y) =>
            SymbolEqualityComparer.Default.Equals(x.Symbol, y.Symbol);
        public int GetHashCode((INamedTypeSymbol Symbol, string Name) obj) =>
            SymbolEqualityComparer.Default.GetHashCode(obj.Symbol);
    }
}
