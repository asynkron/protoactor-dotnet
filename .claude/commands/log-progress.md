---
description: Log progress to GitHub issues (find, create, update)
allowed-tools: Bash(gh:*)
argument-hint: [issue-number] [message]
---

Log session progress to GitHub issues. Issues serve as persistent working memory across sessions.

For full workflow details, see [agents/how-to-workflow-and-issues.md](../../agents/how-to-workflow-and-issues.md).

## Workflow

### 1. Find or create an issue

Search for existing issues:
```bash
gh issue list -S "keyword"
gh issue view <number>
```

Create if none fit:
```bash
gh issue create -t "Title" -b "Context, plan, next steps"
```

### 2. Log progress

Add a comment with your progress:
```bash
gh issue comment <number> -b "Update text"
```

Or update an existing comment:
```bash
gh api --method PATCH repos/asynkron/Asynkron.JsEngine/issues/comments/<comment_id> -f body="$(cat /tmp/body.txt)"
```

### 3. Link related work

Use markdown references in comments:
- `Related to #344`
- `Blocked by #123`
- `Fixes #456`

## What to Include

Always summarize:
- **Changes made** - What was modified
- **Remaining work** - What's left to do
- **Test results** - Pass/fail status
- **Blockers** - Any issues encountered

This ensures the next session can resume quickly.

## Examples

Log progress to issue #123:
```
/log-progress 123 "Implemented the lexer changes, tests passing. Next: update parser."
```

Find issues about generators:
```
/log-progress
# Then search: gh issue list -S "generator"
```
