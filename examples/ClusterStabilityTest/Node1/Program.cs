// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace TestApp
{
    static class Program
    {
        private static async Task Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                await Client.Start();
            }
            else
            {
                await Worker.Start(args[0], args[1]);
            }
        }
    }
}