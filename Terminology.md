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

#### Cluster

#### Router

#### Host

#### Port

#### Address

Address means the combination of Host and Port, e.g. localhost:8080

#### Advertised Host

Advertised host, is the host exposed to other members.
A system might "bind" to IP 0.0.0.0, and at the same time advertise e.g. 192.168.0.22 to other members of a cluster.

#### Advertised Port

#### PID

#### Props

#### Actor Context

#### Cluster Context

#### Member
A member of a Proto.Cluster, usually a process running on a separate container/pod/machine.

