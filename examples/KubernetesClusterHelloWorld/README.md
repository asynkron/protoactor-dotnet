This is a short, and maybe not production ready kubernetes sample, but something
to get you started at least. This is also geared towards Google gloud at the
moment, but you can probably tweak it to any kubernetes cluster.

How to test it:

* You might need to build the whole thing first, using the build script at the
  root of the repo.
* Run the local build script, and set project env variable, like such:
  `PROJECT_ID=<your gcp project> ./build.sh`
* Run the deploy script, `PROJECT_ID=<your gcp project> ./deploy.sh`

You should now have a consult cluster running with three nodes in a kubernetes
namespace named `test`. You can check the consul UI by running
`./expose_consul.sh` and the visit http://localhost:8500/.

To check that the actual proto actor apps worked you need to check the logs of
`node1` with the command `k logs node1 --namespace=test`. The log should contain
the row: `==> Result: { "Message": "Hello from node2" }` if it worked as
expected.

When you're done you can clean up everything by running `./cleanup.sh`.
