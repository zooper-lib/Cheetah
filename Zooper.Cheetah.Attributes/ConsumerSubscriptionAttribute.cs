using System;

namespace Zooper.Cheetah.Attributes
{
	/// <summary>
	/// Attribute to specify the subscription name for a consumer.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	// ReSharper disable once ClassNeverInstantiated.Global
	public class ConsumerSubscriptionAttribute : Attribute
	{
		public string? SubscriptionName { get; }

		public ConsumerSubscriptionAttribute(string? subscriptionName = null)
		{
			SubscriptionName = subscriptionName;
		}
	}
}