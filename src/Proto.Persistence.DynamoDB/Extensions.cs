using System;
using System.Linq;
using System.Reflection;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace Proto.Persistence.DynamoDB
{
    public static class Extensions
    {
        private static MethodInfo fromDocumentMi;

        private static MethodInfo toDocumentMi;

        public static DynamoDBEntry GetValueOrThrow(this Document doc, string attribute)
        {
            bool success = doc.TryGetValue(attribute, out DynamoDBEntry entry);

            if (!success)
            {
                throw new InvalidOperationException("Attribute does NOT exists: " + attribute);
            }

            return entry;
        }

        public static object FromDocumentDynamic(this DynamoDBContext ctx, Document doc, Type type)
        {
            if (fromDocumentMi == null)
            {
                fromDocumentMi = typeof(DynamoDBContext).GetMethods()
                    .First(m => m.Name == "FromDocument" && m.GetParameters().Length == 1);
            }

            object obj = fromDocumentMi
                .MakeGenericMethod(type)
                .Invoke(ctx, new object[] {doc});

            return obj;
        }

        public static Document ToDocumentDynamic(this DynamoDBContext ctx, object obj, Type objType)
        {
            if (toDocumentMi == null)
            {
                toDocumentMi = typeof(DynamoDBContext).GetMethods()
                    .First(m => m.Name == "ToDocument" && m.GetParameters().Count() == 1);
            }

            Document doc = toDocumentMi
                .MakeGenericMethod(objType)
                .Invoke(ctx, new[] {obj}) as Document;

            return doc;
        }

        public static string AssemblyQualifiedNameSimple(this Type type)
        {
            string name = type.AssemblyQualifiedName;
            int index1St = name.IndexOf(",");
            if (index1St < 0)
            {
                return name;
            }

            int index2nd = name.IndexOf(",", index1St + 1);
            if (index2nd < 0)
            {
                return name;
            }

            return name.Substring(0, index2nd);
        }
    }
}
