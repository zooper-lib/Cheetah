using System;
using System.Threading.Tasks;
using MassTransit;
using Zooper.Cheetah.Generators.Sample.Events;

namespace Zooper.Cheetah.Generators.Sample.Consumers;

public sealed class AccountCreatedConsumer(ILogger<AccountCreatedConsumer> logger)
	: IConsumer<IAccountSignedUpIntegrationEvent.V1>
{
	public async Task Consume(ConsumeContext<IAccountSignedUpIntegrationEvent.V1> context)
	{
		logger.LogInformation("Received AccountCreatedIntegrationEvent: {@Event}", context.Message);
		await Task.CompletedTask;
	}
}

public sealed class AccountCreatedConsumerV2(ILogger<AccountCreatedConsumerV2> logger)
	: IConsumer<IAccountSignedUpIntegrationEvent.V2>
{
	public async Task Consume(ConsumeContext<IAccountSignedUpIntegrationEvent.V2> context)
	{
		logger.LogInformation("Received AccountCreatedIntegrationEvent: {@Event}", context.Message);
		await Task.CompletedTask;
	}
}

public interface ILogger<T>
{
	void LogInformation(string message, object args);
}