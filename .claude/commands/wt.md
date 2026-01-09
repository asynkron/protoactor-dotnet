---
description: Create a git worktree for a feature (project)
allowed-tools: Bash(git:*)
argument-hint: <featurename>
---

Create a git worktree for the specified feature.

Execute this command:

```bash
git worktree add ../Asynkron.JsEngine-$ARGUMENTS -b feature/$ARGUMENTS
```

After creating the worktree, output this message to the user:

```
Worktree created at ../Asynkron.JsEngine-$ARGUMENTS
Branch: feature/$ARGUMENTS

To work in this worktree, exit and start a new Claude Code session:
  cd ../Asynkron.JsEngine-$ARGUMENTS && claude
```

## Examples

Create a worktree for a new feature:
```
/wt type-narrowing
```

This will:
1. Create `../Asynkron.JsEngine-type-narrowing` worktree
2. Create branch `feature/type-narrowing`
3. Prompt you to start a new Claude Code session there

## Cleanup

When done with the feature (after PR is merged), run from the main repo:
```bash
git pull origin main
git worktree remove ../Asynkron.JsEngine-<featurename> --force
git branch -D feature/<featurename>
```
