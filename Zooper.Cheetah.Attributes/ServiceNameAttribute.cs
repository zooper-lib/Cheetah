using System;

namespace Zooper.Cheetah.Attributes
{
	/// <summary>
	/// Attribute to specify the service name for endpoint configuration.
	/// Used by the endpoint generators to create queue and subscription names.
	/// </summary>
	[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
	public sealed class ServiceNameAttribute : Attribute
	{
		/// <summary>
		/// Gets the name of the service.
		/// </summary>
		public string ServiceName { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ServiceNameAttribute"/> class.
		/// </summary>
		/// <param name="serviceName">The name of the service.</param>
		public ServiceNameAttribute(string serviceName)
		{
			ServiceName = serviceName;
		}
	}
}