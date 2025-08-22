# Agent Instructions

- Verify that .NET 8 is installed by running `dotnet --version`.
- Do not run Docker or other external services; skip integration tests that require them.
- When adding or modifying tests:
  - Prefer Proto.TestKit utilities.
  - Use probes instead of `TaskCompletionSource` or ad-hoc recording/forwarding actors.
  - Assert message contents through `probe.ExpectNext*` methods.
  - Preserve the existing level of assertions; do not reduce coverage.
  - When suitable, use `ExpectEmptyMailbox` rather than `ExpectNoMessages`.

