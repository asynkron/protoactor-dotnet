# RemoteTLS

Demonstrates securing Proto.Remote communication with TLS.

## Certificates

Generate a self-signed development certificate `localhost.pfx` and place it next to this README. For example:

```bash
openssl req -x509 -newkey rsa:2048 -keyout key.pem -out cert.pem -days 365 -nodes -subj "/CN=localhost"
openssl pkcs12 -export -out localhost.pfx -inkey key.pem -in cert.pem -passout pass:password
```

## Running the example

1. Start the secure server:

   ```bash
   dotnet run --project Server
   ```

2. From another terminal, run the client:

   ```bash
   dotnet run --project Client
   ```

The client validates the server certificate by comparing thumbprints and prints the greeting returned by the remote actor.
