# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.3] - 2026-08-15

### Fixed

- `Server.GetClients()` contains the client while its `ClientConnected` event runs. It used to be added after the event, so a handler counting clients was one short.
- A client that disconnects while a controller method is still running no longer crashes the server process.
- A failed listener no longer reports `Listening` as `true`: a bind error thrown from the setter, and an accept error raises `ClientConnectingError` and resets the property.
- An enum value sent as a message arrives as that enum. It used to break the connection.
- `SendToAllAsync` delivers the message to the remaining clients when one of them disconnects mid-send. It used to stop at the first disconnected client.
- A `DateTime` keeps its `Kind` in transit. A UTC value used to arrive converted to local time.
- Sending while the connection is being disposed throws `NetworkConnectionBreakException` instead of `NullReferenceException`.

## [1.0.2] - 2024-07-31

### Fixed

- A controller method returning `Task<T>` gave `null` instead of the result.

## [1.0.1] - 2024-07-30

### Added

- `Server.ListenAsync()` accepts clients on the calling task, as an alternative to the background thread started by `Listening`.

### Fixed

- A controller method returning `Task<T>` gave a wrong result.

## [1.0.0] - 2023-07-04

First release. TCP client and server exchanging serializable objects, remote method invocation through controller interfaces, authentication with `ClientCredentials`, SSL support and a replaceable serializer.

[1.0.2]: https://github.com/TagBites/TagBites.Net/compare/1.0.1...1.0.2
[1.0.1]: https://github.com/TagBites/TagBites.Net/compare/1.0.0...1.0.1
[1.0.0]: https://github.com/TagBites/TagBites.Net/releases/tag/1.0.0
