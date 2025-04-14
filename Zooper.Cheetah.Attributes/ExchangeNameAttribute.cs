using System;

namespace Zooper.Cheetah.Attributes
{
	[AttributeUsage(
		AttributeTargets.Class,
		Inherited = false
	)]
	public sealed class ExchangeNameAttribute : Attribute
	{
		public string ExchangeName { get; }

		public ExchangeNameAttribute(string exchangeName)
		{
			ExchangeName = exchangeName;
		}
	}
}