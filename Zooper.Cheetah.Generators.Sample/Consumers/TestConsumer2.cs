using MassTransit;
using Zooper.Cheetah.Generators.Sample.Events;

namespace Zooper.Cheetah.Generators.Sample.Consumers;

public class TestConsumer2 : IConsumer<TestEventTwo>
{
	public async Task Consume(ConsumeContext<TestEventTwo> context)
	{
		throw new NotImplementedException();
	}
}