// -----------------------------------------------------------------------
// <copyright file="DiagnosticsSerializer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Reflection;
using System.Text;

namespace Proto.Diagnostics
{
    public static class DiagnosticsSerializer
    {
        public static string Serialize(IActor actor)
        {
            var sb = new StringBuilder();
            var fields = actor.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                
                sb.Append(field.Name);
                sb.Append(" = ");

                try
                {
                    var value = field.GetValue(actor);
                    sb.Append(value);
                }
                catch
                {
                    sb.Append("Error reading value");
                }

                sb.AppendLine();
            }
            
            return sb.ToString();
        }
    }
}