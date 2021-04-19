using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf.Reflection;
using ProtoBuf.Reflection;
using Xunit;
using Xunit.Abstractions;
using CodeGenerator = Proto.GrainGenerator.CodeGenerator;

namespace ProtoGrainGenerator.Tests
{
    public class ProtoGrainGenerationTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public ProtoGrainGenerationTests(ITestOutputHelper testOutputHelper) => _testOutputHelper = testOutputHelper;

        [Fact]
        public void CanFindImportedNamespaces()
        {
            var r = new FileInfo("foo.proto").OpenText();
            var set = new FileDescriptorSet();
            set.AddImportPath(".");
            set.Add("foo.proto", true, r);
            set.Process();
            var c = new CodeGenerator();
            var res = c.Generate(set, NameNormalizer.Default, new Dictionary<string, string>()).ToArray();

            foreach (var codeFile in res)
            {
                _testOutputHelper.WriteLine(codeFile.Text);
            }

        }
    }
}