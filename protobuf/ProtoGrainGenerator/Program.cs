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
            try
            {
                if (args.Length < 1)
                {
                    Console.WriteLine("You need to specify a path to the proto file to use");
                }
                else
                {
                    var set = new FileDescriptorSet();
                    
                    var r = File.OpenText($@"{args[0]}");

                    var defaultOutputName = Path.GetFileName(args[0]);

                    if (args.Length > 1)
                    {
                        defaultOutputName = args[1];
                    }

                    set.Add(defaultOutputName, true, r);
                    
                    set.Process();

                    var gen = new GrainGen();
                    var res = gen.Generate(set).ToList();

                    foreach(var items in res)
                    {
                        File.WriteAllText(items.Name, items.Text);
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}