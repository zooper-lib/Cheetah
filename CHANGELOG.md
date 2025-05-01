# Changelog

All notable changes to the Zooper.Cheetah project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2025-05-01

### Added
- Dead letter queue support in generated receive endpoints
  - Added `ConfigureDeadLetterQueueDeadLetterTransport()` method call
  - Added `ConfigureDeadLetterQueueErrorTransport()` method call
  - Added optional parameter `enableDeadLettering` (defaults to true) to control dead letter functionality

### Changed
- Refactored `ReceiveEndpointSourceGenerator` to remove magic strings, improving code maintainability
  - Extracted string constants for namespace names, file names, and other parameters
  - Set proper value for `FileName` constant as "ReceiveEndpointExtensions"

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