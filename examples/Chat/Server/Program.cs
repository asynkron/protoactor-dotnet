using Proto;

class Program
{
    static void Main(string[] args)
    {
        var props = Actor.FromFunc(ctx =>
        {
            var msg = ctx.Message;
            switch (msg)
            {
                
            }
            return Actor.Done;
        });
    }
}