using System;

namespace Zooper.Cheetah.Attributes
{
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	public sealed class ConsumerAttribute : Attribute
	{
		/// <summary>
		/// The name of the messaging channel (e.g., topic, exchange).
		/// </summary>
		public string ChannelName { get; }

		/// <summary>
		/// The name of the endpoint (e.g., subscription, queue) where the consumer listens.
		/// </summary>
		public string EndpointName { get; }

		public ConsumerAttribute(string channelName, string endpointName)
		{
			ChannelName = channelName;
			EndpointName = endpointName;
		}
	}
}