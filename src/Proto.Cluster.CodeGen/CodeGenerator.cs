// -----------------------------------------------------------------------
// <copyright file="GrainGen.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using Google.Protobuf.Reflection;
using HandlebarsDotNet;
using Proto.Cluster.CodeGen.model;
using ProtoBuf.Reflection;

namespace Proto.Cluster.CodeGen
{
    public class CodeGenerator : CommonCodeGenerator
    {
        private readonly string _template;

        public CodeGenerator(string template)
        {
            _template = template;
        }

        public override string Name => "Proto.Grain";

        protected override string DefaultFileExtension => ".cs";

        protected override string Escape(string identifier) => identifier;

        protected override void WriteFile(GeneratorContext ctx, FileDescriptorProto obj)
        {
            var file = ctx.File;

            var ast = new ProtoFile
            {
                Filename = file.Name,
                PackageName = file.Package,
                CsNamespace = file.Options?.CsharpNamespace ?? file.Package,
                Messages = file
                    .MessageTypes
                    .ToArray()
                    .Select(mt => new ProtoMessage
                        {
                            Name = mt.Name,
                            Fields = mt.Fields.Select(f => new ProtoField()
                                {
                                    TypeName = f.TypeName,
                                    Name = f.Name,
                                    Number = f.Number,
                                    IsRepeated = f.label == FieldDescriptorProto.Label.LabelRepeated,
                                    OneOfIndex = f.OneofIndex,
                                    Type = f.type,
                                    Object = ctx.TryFind<DescriptorProto>(f.TypeName),
                                }
                            ).ToArray()
                        }
                    )
                    .ToArray(),
                Services = file
                    .Services
                    .ToArray()
                    .Select(
                        s => new ProtoService
                        {
                            Name = s.Name,
                            Methods = s.Methods.ToArray()
                                .Select(
                                    (m, i) => new ProtoMethod
                                    {
                                        Index = i,
                                        Name = m.Name,
                                        InputNameRaw = RemovePackageName(m.InputType),
                                        OutputNameRaw = RemovePackageName(m.OutputType),
                                        InputObject = ctx.TryFind<DescriptorProto>(m.InputType),
                                        OutputObject = ctx.TryFind<DescriptorProto>(m.OutputType),
                                        EmptyReturn = m.OutputType.Equals(".google.protobuf.Empty"),
                                        EmptyParameter = m.InputType.Equals(".google.protobuf.Empty")
                                    }
                                )
                                .ToArray()
                        }
                    )
                    .ToArray()
            };

            Handlebars.RegisterHelper("StringEquality", (output, options, context, arguments) => {
                    if (arguments.Length != 2)
                    {
                        throw new HandlebarsException("{{#StringEquality}} helper must have exactly two arguments");
                    }

                    var left = arguments.At<string>(0);
                    var right = arguments[1] as string;
                    if (left == right) options.Template(output, context);
                    else options.Inverse(output, context);
                }
            );

            var f = Handlebars.Compile(_template);

            var result = f(ast);
            ctx.WriteLine(result);

            static string RemovePackageName(ReadOnlySpan<char> type)
            {
                var index = type.LastIndexOf('.');
                var res = type[(index + 1)..].ToString();

                if (res == "")
                {
                    return "MissingName" + type.ToString();
                }

                return res;
            }
        }

        #region UnusedMethods

        protected override void WriteNamespaceHeader(GeneratorContext ctx, string @namespace) => throw new NotImplementedException();

        protected override void WriteNamespaceFooter(GeneratorContext ctx, string @namespace) => throw new NotImplementedException();

        protected override void WriteField(
            GeneratorContext ctx,
            FieldDescriptorProto obj,
            ref object state,
            OneOfStub[] oneOfs
        ) => throw new NotImplementedException();

        protected override void WriteMessageFooter(GeneratorContext ctx, DescriptorProto obj, ref object state)
            => throw new NotImplementedException();

        protected override void WriteMessageHeader(GeneratorContext ctx, DescriptorProto obj, ref object state)
            => throw new NotImplementedException();

        protected override void WriteEnumHeader(GeneratorContext ctx, EnumDescriptorProto obj, ref object state)
            => throw new NotImplementedException();

        protected override void WriteEnumValue(GeneratorContext ctx, EnumValueDescriptorProto obj, ref object state)
            => throw new NotImplementedException();

        protected override void WriteEnumFooter(GeneratorContext ctx, EnumDescriptorProto obj, ref object state)
            => throw new NotImplementedException();

        #endregion
    }
}