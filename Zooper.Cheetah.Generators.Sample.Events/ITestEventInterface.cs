using MassTransit;

namespace Zooper.Cheetah.Generators.Sample.Events;

public interface ITestEventInterface
{
	[EntityName("test-event-interface-one")]
	public sealed record TestEventInterfaceOne : ITestEventInterface;

	[EntityName("test-event-interface-two")]
	public sealed record TestEventInterfaceTwo : ITestEventInterface;
}