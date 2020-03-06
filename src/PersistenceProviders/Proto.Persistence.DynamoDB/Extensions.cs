using System;
using System.Linq;
using System.Reflection;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace Proto.Persistence.DynamoDB
{
    public static class Extensions
    {
        public static DynamoDBEntry GetValueOrThrow(this Document doc, string attribute)
        {
            var success = doc.TryGetValue(attribute, out var entry);

            if (!success)
            {
                throw new InvalidOperationException("Attribute does NOT exists: " + attribute);
            }

            return entry;
        }

        private static MethodInfo fromDocumentMi;

        public static object FromDocumentDynamic(this DynamoDBContext ctx, Document doc, Type type)
        {
            if (fromDocumentMi == null)
            {
                fromDocumentMi = typeof(DynamoDBContext).GetMethods().First(m => m.Name == "FromDocument" && m.GetParameters().Length == 1);
            }

            var obj = fromDocumentMi
                .MakeGenericMethod(type)
                .Invoke(ctx, new object[] {doc});

            return obj;
        }

        private static MethodInfo toDocumentMi;

        public static Document ToDocumentDynamic(this DynamoDBContext ctx, object obj, Type objType)
        {
            if (toDocumentMi == null)
            {
                toDocumentMi = typeof(DynamoDBContext).GetMethods().First(m => m.Name == "ToDocument" && m.GetParameters().Count() == 1);
            }

            var doc = toDocumentMi
                .MakeGenericMethod(objType)
                .Invoke(ctx, new[] {obj}) as Document;

            return doc;
        }

        public static string AssemblyQualifiedNameSimple(this Type type)
        {
            var name = type.AssemblyQualifiedName;
            var index1St = name.IndexOf(",");
            if (index1St < 0) return name;

            var index2nd = name.IndexOf(",", index1St + 1);
            if (index2nd < 0) return name;

            return name.Substring(0, index2nd);
        }
    }
}