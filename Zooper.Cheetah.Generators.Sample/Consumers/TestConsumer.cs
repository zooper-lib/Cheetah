using MassTransit;
using Zooper.Cheetah.Attributes;
using Zooper.Cheetah.Generators.Sample.Events;

namespace Zooper.Cheetah.Generators.Sample.Consumers;

//[Consumer("TestTopic", "TestSubscription")]
public class TestConsumer : IConsumer<TestEventOne>
{
	public Task Consume(ConsumeContext<TestEventOne> context)
	{
		return Task.CompletedTask;
	}
}