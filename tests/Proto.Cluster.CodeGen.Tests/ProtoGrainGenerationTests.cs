using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf.Reflection;
using ProtoBuf.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.CodeGen.Tests
{
    public class ProtoGrainGenerationTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public ProtoGrainGenerationTests(ITestOutputHelper testOutputHelper) => _testOutputHelper = testOutputHelper;

        [Theory]
        [InlineData("foo.proto", "ExpectedOutput.cs")]
        public void CanGenerateGrains(string protoDefinitionFile, string expectedOutputFile)
        {
            var r = new FileInfo(protoDefinitionFile).OpenText();
            var set = new FileDescriptorSet();
            set.AddImportPath(".");
            set.Add(protoDefinitionFile, true, r);
            set.Process();
            var c = new CodeGenerator(Template.DefaultTemplate);
            var res = c.Generate(set, NameNormalizer.Default, new Dictionary<string, string>()).ToArray();

            foreach (var codeFile in res)
            {
                _testOutputHelper.WriteLine(codeFile.Text);
            }

            var expectedOutput = File.ReadAllText(expectedOutputFile).Trim();
            Assert.Equal(expectedOutput, res.Single().Text.Trim());
        }
    }
}