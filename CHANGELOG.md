# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- `Server.GetClients()` contains the client while its `ClientConnected` event runs. It used to be added after the event, so a handler counting clients was one short.

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
