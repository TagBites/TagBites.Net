---
title: Error handling
description: Events which report failures, and reconnecting after a lost connection.
---

Failures while receiving surface through `ReceivedError`, a lost connection through `Disconnected` (`ClientDisconnected` on the server), and errors while accepting a client through `ClientConnectingError`.

## Handling receive errors

```csharp
client.ReceivedError += (s, e) =>
{
    // Inspect e.Exception to distinguish transport errors
    // from serialization/deserialization errors.
    Console.WriteLine($"Failed to receive message: {e.Exception}");
};
```

On `Client` this is `NetworkConnectionMessageErrorEventArgs` with a single `Exception` property. On `Server`/`ServerClient` it's `ServerClientMessageErrorEventArgs`, which adds `Exception` alongside the inherited `Client` (the `ServerClient` that sent the offending message).

## Handling disconnects and reconnecting

The library does not reconnect automatically - the consumer implements it:

```csharp
async Task ConnectWithRetryAsync(Client client, int maxAttempts = 5)
{
    var delay = TimeSpan.FromSeconds(1);

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await client.ConnectAsync();
            return;
        }
        catch (NetworkConnectionOpenException ex) when (attempt < maxAttempts)
        {
            Console.WriteLine($"Connect attempt {attempt} failed: {ex.Message}");
            await Task.Delay(delay);
            delay += delay; // simple backoff
        }
    }
}

client.Disconnected += async (s, e) =>
{
    Console.WriteLine("Disconnected, attempting to reconnect...");
    await ConnectWithRetryAsync(client);
};
```

The same `Client` instance can be reused: after `Disconnected` fires, `IsConnected` becomes `false` again, so calling `ConnectAsync()` (or `ConnectSslAsync()`) on the same instance opens a fresh connection. You do not need to create a new `Client` object to reconnect, so the retry helper above is safe to call repeatedly on the same `client` reference.
