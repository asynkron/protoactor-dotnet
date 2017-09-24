using System;
using System.Linq;
using System.Reflection;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace Proto.Persistence.DynamoDB
{
    public static class Extensions
    {
        public static DynamoDBEntry GetValueOrEx(this Document doc, string attribute)
        {
            DynamoDBEntry entry;
            var success = doc.TryGetValue(attribute, out entry);
            if (!success)
            {
                throw new InvalidOperationException("Attribute does NOT exists: " + attribute);
            }
            return entry;
        }

        private static MethodInfo FromDocumentMI = null;
        public static object FromDocumentDynamic(this DynamoDBContext ctx, Document doc, Type type)
        {
            if (FromDocumentMI == null)
            {
                FromDocumentMI = typeof(DynamoDBContext).GetMethods().First(m => m.Name == "FromDocument" && m.GetParameters().Count() == 1);
            }

            var obj = FromDocumentMI
                .MakeGenericMethod(type)
                .Invoke(ctx, new [] {doc});

            return obj;
        }

        private static MethodInfo ToDocumentMI = null;
        public static Document ToDocumentDynamic(this DynamoDBContext ctx, object obj, Type objType)
        {
            if (ToDocumentMI == null)
            {
                ToDocumentMI = typeof(DynamoDBContext).GetMethods().First(m => m.Name == "ToDocument" && m.GetParameters().Count() == 1);
            }
            
            var doc = ToDocumentMI
                .MakeGenericMethod(objType)
                .Invoke(ctx, new [] {obj}) as Document;
            
            return doc;
        }

        public static string AssemblyQualifiedNameSimple(this Type type)
        {
            var name = type.AssemblyQualifiedName;
            var index1st = name.IndexOf(",");
            if (index1st < 0) return name;
            var index2nd = name.IndexOf(",", index1st + 1);
            if (index2nd < 0) return name;
            return name.Substring(0, index2nd);
        }

    }
}