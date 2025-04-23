using MassTransit;
using Zooper.Cheetah.Generators.Sample.Events;

namespace Zooper.Cheetah.Generators.Sample.Consumers;

public class TestInterfaceOneConsumer : IConsumer<ITestEventInterface.TestEventInterfaceOne>
{
	public Task Consume(ConsumeContext<ITestEventInterface.TestEventInterfaceOne> context)
	{
		return Task.CompletedTask;
	}
}