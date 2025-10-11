# Agent Instructions

## Setup
- Verify that .NET 8 is installed by running `dotnet --version`.
- Do not run Docker or other external services; skip integration tests that require them.

## Knowledge Base (`context.md`)
* Every directory in this repository contains a `context.md` that summarises the purpose of the files underneath it. 
* Whenever you modify code, tests, assets, or configuration within a directory, update that directory's `context.md` (and any parent summaries if the high-level description needs to change). Keep the links between related contexts accurate.
* Always start a new session by running the following command:
```
find . -name context.md -print0 | sort -z | xargs -0 -I{} sh -c 'echo "## {}"; cat "{}";
  │ echo' > /tmp/context_dump.txt
```
Read the dump file, understand the meaning, the structures, the architecture, anything of value, and then delete it.

## Live-View MCP
- if live-view MCP is present, use it as a way to notify the user of what you are doing.
- you can use the `create_markdown_file` function to create a markdown file that will be rendered in the live-view MCP.
- e.g. describe architectureal diagrams highlight snippets of relevant code etc.
- use it as if it was a markdown based chat interface to the user

## Always follow these coding guidelines
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
