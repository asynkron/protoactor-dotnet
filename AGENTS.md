# Agent Instructions

## Setup
- Verify that .NET 8 is installed by running `dotnet --version`.
- Do not run Docker or other external services; skip integration tests that require them.

## Always follow these coding guidelines
- Always add a detailed log of what you have done, and a strong motivation why the change was required. add this log to /logs with a filename of "log" + unixtimestamp + ".md".
- Every directory now contains a `context.md` knowledge card. Before working in any area, review the closest `context.md` files to understand the subsystem, and keep them up to date with any relevant changes.
- When touching any files in a directory, update that directory's `context.md` (and parent contexts if the change alters their overview) to reflect the new information.
- Whenever a prompt contains an .NET exception, document this specific exception in /logs/exceptions.md, failed test name as ### header, important details about the failure as `code`, so we can keep track of failures. if exceptions.md already exists, just append at the end
- Prefer immutable data structures over mutable variants
- Prefer Concurrent collections over Immutable collections when dealing with concurrent code, but don´t replace for no reason.
- Any hardcoded Task.Delay must have a descriptive comment
- Prefer functional programming style over object orientation when it makes sense.
- Ensure any new code is also tested via some code path, either existing tests or via new tests
- In the framework code, all logging via ILogger should be "typed logging", no raw logging string templates

## Refactoring
- For computational logic, prefer pure functions, if possible in static classes, with easily testable input and output. e.g. Gossip and Cluster Topology logic are good examples.

## Testing
- Freely suggest new helpers for Proto.TestKit if we detect a pattern that can be simplified in many tests
- Always run the core tests, Proto.Actor.Tests, Proto.Remote.Tests, the base Proto.Cluster.Tests. if they fail, you have failed.
- When adding or modifying tests:
  - Prefer Proto.TestKit utilities when possible.
  - Prefer `TestProbes` or `TestMailboxStats` instead of `TaskCompletionSource` when the interaction depends on actor messages
  - Prefer `TestProbes` or `TestMailboxStats` instead of ad-hoc recording/forwarding actors.
  - Assert message contents through `probe.ExpectNext*` methods.
  - Preserve the existing level of assertions; do not reduce coverage.
  - Prefer `ExpectEmptyMailbox` over `ExpectNoMessages`.
