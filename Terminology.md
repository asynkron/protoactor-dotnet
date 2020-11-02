# Proto.Actor Terminology

#### Actor

A seemingly single threaded unit of computation. actors process a single message at a time, and thus make them easy/easier to reason about internally.
Scaling with actors is done by spawning many actors to do concurrent work.

#### Virtual Actor

Virtual Actors is a concept pioneered by Project Orleans, these are actors that live somewhere in a cluster. and appears to always exist.
You access such actors by identity/name, if they do not already exist, the cluster will spawn them for you, transparently, there and then.

#### Grain

Grains are a synomym with Virtual Actors, in Project Orleans, Grains live inside a Silo (a host).

#### Remote

Proto.Remote, is the networking layer that makes process to process communication possible.
It basically provides the actor system to resolve `PIDs` that point to another address than the local system does.

#### Cluster

Proto.Cluster, is a orechestration mechanism built ontop of Proto.Remote, it makes it possible to work with Virtual Actors. see above.

#### Router

A Router, is an actor like object which helps distribute messages to different target actors.
There are various different routing strategies implemented. e.g. Round Robin or Consistent Hash routing.

#### Host

The host part of an address. e.g. localhost, 127.0.0.1, someKubernetesService, 192.168.0.22 etc.

#### Port

The port part of an address.

#### Address

Address means the combination of Host and Port, e.g. localhost:8080

#### Advertised Host

Advertised host, is the host exposed to other members.
A system might "bind" to IP 0.0.0.0, and at the same time advertise e.g. 192.168.0.22 to other members of a cluster.

#### Advertised Port

Advertised Port, is the port exposed to other members.
You might bind to one internal port, but might expose a different port outwards. e.g. in Kubernetes or using some proxy layer between your Proto members.

#### PID

PID, Process ID, you can think of this as the phone number to an actor.
You can call the actor from any locations, locally or from remote.

#### Props

Props, a play on words from the Akka framework.
**Actors needs props to act.**

The Props describe how an actor is created, it is basically the blueprint on how to assemble an actor.
What producer to use, what mailbox to use, what supervision strategy to use etc.

#### Actor Context

Actor context is the internal state of an actor. the actor context exists for as long as the actor exists.

#### Cluster Context

#### Member
A member of a Proto.Cluster, usually a process running on a separate container/pod/machine.

