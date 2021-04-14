using System;
using System.Linq;
using Grpc.Core;
using Grpc.Net.Client;
using Proto.Remote;
using Spectre.Console;

namespace Proto.CLI
{
    public static class Extensions
    {
        public static void GetArg(this string[] args, int element, Action<string> action, Action missing = null)
        {
            if (element >= args.Length)
            {
                missing?.Invoke();
                return;
            }

            action(args[element]);
        }
    }

    class Program
    {
        static void Main()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var address = AnsiConsole.Ask<string>("Connect to address: ");

            var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions()
                {
                    Credentials = ChannelCredentials.Insecure,
                }
            );

            var client = new Remoting.RemotingClient(channel);
            AnsiConsole.MarkupLine($"Using address [green]{address}[/]");
            AnsiConsole.Write("Find PIDs matching this pattern?");
            var part = Console.ReadLine();
            var res = client.ListProcesses(new ListProcessesRequest()
                {
                    Name = part
                }
            );

            var pids = res.Pids.Select(pid => pid.Id).OrderBy(id => id).ToArray();
            var id = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .PageSize(20)
                    .Title("Found the following PIDs")
                    .MoreChoicesText("[grey](Move up and down to reveal more PIDs)[/]")
                    .AddChoices(pids)
            );
            
            AnsiConsole.MarkupLine($"Selected [green]{id}[/]");
            var pid = new PID()
            {
                Address = address.Replace("http://",""),
                Id = id,
            };
            var diagnostics = client.GetProcessDiagnostics(new GetProcessDiagnosticsRequest()
                {
                    Pid = pid
                }
            );

            var rows = diagnostics
                .DiagnosticsString
                .Replace("[","(")
                .Replace("]",")")
                .Split("\n", StringSplitOptions.TrimEntries).Skip(1)
                .Select(row => row.Split(" = ",StringSplitOptions.RemoveEmptyEntries))
                .Where(r => r.Length == 2)
                .OrderBy(r => r[0])
                .ToArray();

            var table = new Table()
                .RoundedBorder()
                .BorderColor(Color.Grey)
                .AddColumn("Field")
                .AddColumn("Value");
            
            foreach (var row in rows)
            {
                table.AddRow(row);
            }

            AnsiConsole.Render(table);
            
        }
    }
}