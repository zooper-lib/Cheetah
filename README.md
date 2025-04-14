# Zooper.Cheetah

A set of source generators for MassTransit message handling, supporting both Azure Service Bus and RabbitMQ.

## Packages

- `Zooper.Cheetah.Generators.AzureServiceBus` - Source generator for Azure Service Bus message handling
- `Zooper.Cheetah.Generators.RabbitMq` - Source generator for RabbitMQ message handling

## Installation

### Azure Service Bus

```bash
dotnet add package Zooper.Cheetah.Generators.AzureServiceBus
```

### RabbitMQ

```bash
dotnet add package Zooper.Cheetah.Generators.RabbitMq
```

## Usage

### 1. Define Your Message Contracts

Create a class to represent your message:

```csharp
using Zooper.Cheetah.Attributes;

[Channel("orders")]  // For Azure Service Bus, this defines the topic name
[ExchangeName("orders")]  // For RabbitMQ, this defines the exchange name
public class OrderCreated
{
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; }
    public decimal TotalAmount { get; set; }
}
```

### 2. Create a Message Handler

#### For Azure Service Bus:

```csharp
using MassTransit;
using Zooper.Cheetah.Attributes;

[Consumer("orders", "order-created-handler")]  // Topic name and subscription name
public class OrderCreatedHandler : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        // Your message handling logic here
        Console.WriteLine($"Order {context.Message.OrderId} created for {context.Message.CustomerName}");
    }
}
```

#### For RabbitMQ:

```csharp
using MassTransit;
using Zooper.Cheetah.Attributes;

[Consumer("orders", "order-created-queue")]  // Exchange name and queue name
public class OrderCreatedHandler : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        // Your message handling logic here
        Console.WriteLine($"Order {context.Message.OrderId} created for {context.Message.CustomerName}");
    }
}
```

### 3. Configure MassTransit

#### Azure Service Bus Configuration:

```csharp
services.AddMassTransit(x =>
{
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host("your-connection-string");
        
        // The source generator will automatically configure your message handlers
        cfg.ConfigureEndpoints(context);
    });
});
```

#### RabbitMQ Configuration:

```csharp
services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        
        // The source generator will automatically configure your message handlers
        cfg.ConfigureEndpoints(context);
    });
});
```

## Features

- Automatic message handler registration
- Type-safe message handling
- Support for both Azure Service Bus and RabbitMQ
- Source-generated code for better performance
- No runtime reflection

## Requirements

- .NET 8.0 or later
- MassTransit 8.4.0 or later

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details. 