// See https://aka.ms/new-console-template for more information

using System.Runtime.InteropServices.JavaScript;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Proto.Cluster;

Console.WriteLine("Hello, World!");
var data2 = "abc"; //enter base64 encoded data here
var message = ClusterTopology.Parser.ParseFrom(ByteString.FromBase64(data2));
Console.WriteLine(message);