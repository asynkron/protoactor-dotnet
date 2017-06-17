using System;
using System.IO;
using System.Linq;
using Google.Protobuf.Reflection;
using GrainGenerator;

namespace ProtoGrainGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            var set = new FileDescriptorSet();
            var r = File.OpenText(@"c:\git\protoactor-dotnet\examples\ClusterGrainHelloWorld\Messages\Protos.proto");
            set.Add("my.proto", true, r);

            set.Process();
            var gen = new GrainGen();
            var res = gen.Generate(set).ToList();
        }
    }
}