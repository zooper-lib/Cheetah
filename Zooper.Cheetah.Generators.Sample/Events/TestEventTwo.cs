using MassTransit;
using Zooper.Cheetah.Attributes;

namespace Zooper.Cheetah.Generators.Sample.Events;

[Channel("test-topic-two")]
[EntityName("test-event-two")]
public sealed record TestEventTwo;