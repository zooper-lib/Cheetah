using System;
using MassTransit;

namespace Zooper.Cheetah.Generators.Sample.Events;

public interface IIntegrationEvent { }

[ExcludeFromTopology]
public interface IAccountSignedUpIntegrationEvent : IIntegrationEvent
{
	public const string EventName = "account-signed-up";

	[EntityName($"{EventName}-v1")]
	public sealed record V1(
		Guid AccountId,
		int Role,
		string Email,
		string Password,
		string UserName,
		string FirstName,
		string LastName) : IAccountSignedUpIntegrationEvent;

	[EntityName($"{EventName}-v2")]
	public sealed record V2(
		Guid AccountId,
		int Role,
		string Email,
		string Password,
		string UserName,
		string FirstName,
		string LastName) : IAccountSignedUpIntegrationEvent;
}