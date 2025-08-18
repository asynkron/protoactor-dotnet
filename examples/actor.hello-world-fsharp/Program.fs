open System
open Proto

type HelloActor() =
    interface IActor with
        member __.ReceiveAsync ctx =
            match ctx.Message with
            | :? {| Who : string |} as hello ->
                printfn "Hello %s" hello.Who
                exit 0
            | _ -> ()
            Actor.Done

[<EntryPoint>]
let main argv =
    let system = ActorSystem()
    let ctx = RootContext(system)
    let props = Props.FromProducer(fun () -> HelloActor() :> IActor)
    let pid = ctx.Spawn props
    ctx.Send(pid, {| Who = "ProtoActor" |})
    Console.ReadLine() |> ignore
    0
