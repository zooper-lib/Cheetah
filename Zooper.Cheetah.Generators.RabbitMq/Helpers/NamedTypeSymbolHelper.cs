using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Zooper.Cheetah.Generators.Helpers;

public static class NamedTypeSymbolHelper
{
	/// <summary>
	/// Recursively searches the global namespace for a type with the specified name.
	/// </summary>
	public static INamedTypeSymbol? FindTypeByName(
		Compilation compilation,
		string typeName)
	{
		var globalNamespace = compilation.GlobalNamespace;
		var queue = new Queue<INamespaceSymbol>();
		queue.Enqueue(globalNamespace);

		while (queue.Count > 0)
		{
			var currentNamespace = queue.Dequeue();

			foreach (var member in currentNamespace.GetMembers())
			{
				switch (member)
				{
					case INamespaceSymbol namespaceMember:
						queue.Enqueue(namespaceMember);
						break;
					case INamedTypeSymbol typeMember when typeMember.Name == typeName:
						return typeMember;
				}
			}
		}

		return null;
	}
}