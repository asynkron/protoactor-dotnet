using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using static Proto.Remote.GrpcNet.GrpcNetRemoteConfig;
using Common;

var certificate = new X509Certificate2("../localhost.pfx", "password");
var handler = new HttpClientHandler();
handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) =>
    cert != null && cert.Thumbprint == certificate.Thumbprint;

var remoteConfig = BindToLocalhost().WithTLS(httpHandler: handler);

var system = new ActorSystem().WithRemote(remoteConfig);
await system.Remote().StartAsync();

var pid = PID.FromAddress("127.0.0.1:8000", "greeter");
var response = await system.Root.RequestAsync<HelloResponse>(pid, new HelloRequest("TLS"));
Console.WriteLine(response.Message);
