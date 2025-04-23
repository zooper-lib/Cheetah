using MassTransit;
using Zooper.Cheetah.Generators.Sample.Events;

namespace Zooper.Cheetah.Generators.Sample.Consumers;

public class TestInterfaceTwoConsumer : IConsumer<ITestEventInterface.TestEventInterfaceTwo>
{
	public Task Consume(ConsumeContext<ITestEventInterface.TestEventInterfaceTwo> context)
	{
		return Task.CompletedTask;
	}
}