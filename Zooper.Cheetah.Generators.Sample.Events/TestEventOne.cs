using MassTransit;

namespace Zooper.Cheetah.Generators.Sample.Events;

//[Channel("test-topic-one")]
[EntityName("test-event-one")]
public sealed record TestEventOne { }