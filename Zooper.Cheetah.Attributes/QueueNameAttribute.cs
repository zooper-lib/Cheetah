using System;

namespace Zooper.Cheetah.Attributes
{
	[AttributeUsage(
		AttributeTargets.Class,
		Inherited = false
	)]
	public sealed class QueueNameAttribute : Attribute
	{
		public string QueueName { get; }

		public QueueNameAttribute(string queueName)
		{
			QueueName = queueName;
		}
	}
}