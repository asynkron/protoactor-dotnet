using System.CommandLine;
using System.Threading.Tasks;

namespace ProtoGrainGenerator
{
    class Program
    {
        static Task Main(string[] args) => Commands.CreateCommands().InvokeAsync(args);
    }
}
