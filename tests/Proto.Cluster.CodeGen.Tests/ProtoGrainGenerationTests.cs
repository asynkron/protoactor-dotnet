using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf.Reflection;
using Proto.Cluster.CodeGen;
using ProtoBuf;
using ProtoBuf.Reflection;
using Xunit;
using Xunit.Abstractions;
using CodeGenerator = Proto.Cluster.CodeGen.CodeGenerator;

namespace Proto.Cluster.CodeGen.Tests
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
            var c = new CodeGenerator(Template.DefaultTemplate);
            var res = c.Generate(set, NameNormalizer.Default, new Dictionary<string, string>()).ToArray();

            foreach (var codeFile in res)
            {
                _testOutputHelper.WriteLine(codeFile.Text);
            }

        }
    }
}