using System;
using System.IO;
using System.Linq;
using Google.Protobuf.Reflection;
using ProtoBuf;

namespace GrainGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            var set = new FileDescriptorSet { AllowImports = false };
            var r = File.OpenText(@"c:\git\protoactor-dotnet\examples\ClusterGrainHelloWorld\Messages\Protos.proto");
            set.Add("my.proto", true, r);

            set.Process();
            var gen = new GrainGen();
            var res = gen.Generate(set).ToList();

        }
    }
}