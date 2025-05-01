# Changelog

All notable changes to the Zooper.Cheetah project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0] - 2025-05-01

### Added
- Added option to control fault publishing with `publishFaults` parameter (defaults to false)

### Changed
- Removed unnecessary comments from generated code for cleaner output

## [1.2.2] - 2025.05.01

### Fixed

- Corrected parameter order in SubscriptionEndpoint method calls (subscription name first, then topic path)
- Updated topic name format to use the entity name directly instead of prefixing with service name

## [1.2.1] - 2025.05.01

### Fixed
- Fixed variable naming in generated endpoints to avoid duplicate declarations
- Corrected SubscriptionEndpoint method signature in generated code
- Added unique identifiers to variable names for multiple consumers

## [1.2.0] - 2025.05.01

### Added
- Dead letter queue support in generated receive endpoints
  - Added `ConfigureDeadLetterQueueDeadLetterTransport()` method call
  - Added `ConfigureDeadLetterQueueErrorTransport()` method call
  - Added optional parameter `enableDeadLettering` (defaults to true) to control dead letter functionality

### Changed
- Changed generated endpoint code to use `SubscriptionEndpoint<T>` instead of `ReceiveEndpoint`
  - Improved clarity with named variables for topic and subscription names
  - Added comments to explain the purpose of each configuration section
- Renamed the generated extension class from `ReceiveEndpointExtensions` to `MassTransitExtensions`
- Refactored `ReceiveEndpointSourceGenerator` to remove magic strings, improving code maintainability
  - Extracted string constants for namespace names, file names, and other parameters
  - Set proper value for `FileName` constant as "MassTransitExtensions"

## [1.1.0] - 2025.04.23

### Added
- Initial implementation of `ReceiveEndpointSourceGenerator`
- Support for code generation of MassTransit receive endpoints
- Integration with Azure Service Bus

## [1.0.0] - 2025.04.14

### Added
- Core attributes for message consumption and routing
- Code generators for RabbitMQ connections
- Basic consumer registration functionality