# Copilot Instructions

## 📋 Documentation Format Standards

### Visual Checkbox Format (MANDATORY)

**Always use visual checkmark characters for all documentation files:**
- **✓** (checkmark) = Completed tasks
- **☐** (empty box) = Pending tasks

**Never use bracket syntax:** `[x]` or `[ ]`

**Applies to:**
- `/docs/IMPLEMENTATION_CHECKLIST.md`
- `/docs/MASTER_PROJECT_PLAN.md`
- All project tracking and status documents
- Any deliverable lists or task checklists

**Why:** Visual format is easier to scan, more professional, and clearer for progress tracking.

---

## � Client/Server Mediation Handoff Protocol (MANDATORY)

**Reference doc:** `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

### Receiving a Relay Message
When the moderator sends a relay (commit hash, "pull main", "check for updates", etc.):
1. `git pull` immediately.
2. Read the **Active Handoff** section autonomously.
3. Execute the described work **without asking questions, giving summaries, or waiting for confirmation**.
4. The handoff doc has everything the agent needs — never ask the moderator for context, permission, or clarification.

### Pre-Commit Checklist (MANDATORY)
Before every commit:
1. Run `git status --short`.
2. **Delete ALL unexpected untracked files/directories** — including gitignored runtime data (e.g., `storage/`, `bin/`, temp files). Only intentional tracked changes should remain.
3. Verify clean state, THEN commit.

### Post-Push Relay (MANDATORY)
After completing work, committing, and pushing:
- **Always provide the ready-to-relay message** for the moderator to send to the other agent.
- Format: `` `<commit-hash>` — New handoff update. Pull and check docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md Active Handoff. ``
- Do NOT make the moderator figure out what to say — give them the exact text.

---

## �🚨 CRITICAL: Complete Work Standards

### The "No Half-Measures" Rule

**PRINCIPLE:** When a user requests work to be done, deliver the COMPLETE work in ONE response, not partial work with "options."

**BAD PATTERNS TO AVOID:**
- ❌ Delivering 40% of requested work and asking "would you like me to continue?"
- ❌ Creating separate/companion documents when user asked for updates to ONE document
- ❌ Giving "options" about how to proceed when user gave clear instructions
- ❌ Worrying about response length instead of completing the task
- ❌ Assuming user preferences that weren't stated

**GOOD PATTERNS:**
- ✅ User asks for X → Deliver complete X in one response
- ✅ If file edits are large → Use edit_file tool multiple times in ONE response
- ✅ If genuinely blocked (technical limitation) → State the blocker upfront, don't guess solutions
- ✅ Follow instructions LITERALLY unless they're ambiguous
- ✅ Take "do it right" seriously → means complete, thorough, professional

**EXAMPLE FAILURE:**
- **Request:** "Complete MASTER_PROJECT_PLAN.md with phases 0.4-0.19"
- **Wrong Response:** Complete 0.4-0.5, create separate doc for 0.6-0.19, give "options"
- **Right Response:** Edit MASTER_PROJECT_PLAN.md directly, add ALL phases 0.4-0.19 in that file

**PROFESSIONALISM STANDARD:**
This is a long-term project where details matter. Treat every request as if you're a paid professional contractor:
- Complete the full scope of work
- Don't make the user repeat themselves
- Don't waste their time with half-done deliverables
- If you're unsure about scope, ASK before starting, don't assume

### The "Token Budget" Fallacy

**WRONG THINKING:** "This response might be long, let me split it up"  
**RIGHT THINKING:** "User asked for complete work. I have 1,000,000 token budget. Deliver complete work."

You have a **massive token budget** (1,000,000 tokens). Use it. Don't artificially limit responses unless you hit ACTUAL technical limits.

---

## 🎯 CRITICAL: Dual Documentation Update Requirement

**⚠️ IMPORTANT:** After EVERY completed implementation task or phase step, you MUST update BOTH documentation files below. Failure to do so will result in incomplete project tracking. This is non-negotiable.

**💡 REMEMBER:** Use TARGETED EDITS (not full file replacement) to preserve Git history!

---

## Project Guidelines

### 1️⃣ Update IMPLEMENTATION_CHECKLIST.md (ALWAYS)

**When:** After completing any Phase 0-9 task or Pre-Implementation step  
**What:** Mark the corresponding checkbox as `✓` for completed tasks and `☐` for pending tasks in `/docs/IMPLEMENTATION_CHECKLIST.md`  
**How:** ⭐ **Use targeted edits (edit_file tool) with minimal context** — do NOT replace the entire file  
**Why:** Provides quick visibility into phase completion status for all stakeholders

**Example (GOOD - Targeted Edit):**
```markdown
### User Authentication
- ✓ Implement user registration endpoint  ← Mark as completed
- ☐ Implement password reset flow         ← Still pending
```

**DO NOT do this (BAD - Full File Replacement):**
```markdown
[Entire file content...]  ← Never do this unless absolutely necessary
```

---

### 2️⃣ Update MASTER_PROJECT_PLAN.md (ALWAYS - DO NOT SKIP!)

**When:** After completing any Phase step (phase-0.1.1, phase-0.2.5, phase-1.3, etc.)  
**What:** 
- Update the **Quick Status Summary** table at the top
- Update the corresponding **Step** details with:
  - `**Status:** completed` (or in-progress, failed, skipped)
  - `**Deliverables:**` list with `✓` marks for completed items and `☐` for pending items
  - `**Notes:**` with current progress and what comes next

**How:** ⭐ **Use targeted edits (edit_file tool) with minimal context FIRST** — only replace entire file as last resort  
**Why:** Preserves Git history, cleaner diffs, easier to review what changed

**Example (GOOD - Targeted Edit):**

Before:
```markdown
| Phase | Steps | Completed | In Progress | Pending |
|-------|-------|-----------|-------------|
| Phase 0.1 | 11 | 3 | 0 | 8 |
```

After (targeted edit):
```markdown
| Phase | Steps | Completed | In Progress | Pending |
|-------|-------|-----------|-------------|
| Phase 0.1 | 11 | 7 | 0 | 4 |
```

And update step section (targeted edit):
```markdown
#### Step: phase-0.1.4 - Event System Interfaces
**Status:** completed ✅
**Duration:** ~1.5 hours
**Deliverables:**
- ✓ `IEvent` base interface
- ✓ `IEventHandler<TEvent>` interface with `Task HandleAsync()` method
- ✓ `IEventBus` interface with PublishAsync, SubscribeAsync, UnsubscribeAsync
- ✓ Event subscription model

**Notes:** Event system complete. Enables inter-module communication via pub/sub pattern.
```

---

## ⭐ Targeted Edits Best Practices (PREFERRED METHOD)

### What are Targeted Edits?
**Targeted edits** use the `edit_file` tool to modify only the specific section that changed, preserving the rest of the file.

### ✅ Benefits of Targeted Edits
1. **Preserves Git History:** Changes are clearly visible in commit diffs
2. **Faster:** Only processes the changed section
3. **Safer:** Less chance of accidentally modifying other content
4. **Professional:** Shows attention to detail
5. **Easier Review:** Reviewers see exactly what changed
6. **Atomic Changes:** One logical change per edit

### ✅ When to Use Targeted Edits
- ✅ Updating status in MASTER_PROJECT_PLAN.md (section at a time)
- ✅ Marking checkboxes in IMPLEMENTATION_CHECKLIST.md
- ✅ Adding a new step or section
- ✅ Updating a table row
- ✅ Adding deliverables to a completed step
- ✅ ANY change that doesn't affect the entire file structure

### ❌ When Full File Replacement is Acceptable
- ❌ Only if targeted edits FAIL multiple times
- ❌ Only if the entire file structure changed significantly
- ❌ Only as LAST RESORT
- ❌ Document WHY in the explanation

### Example: Targeted Edit Format

**Using edit_file tool correctly:**

Input section (what you provide):
```
// ...existing content...
- ☐ Task 1
- ✓ Task 2  ← This is the ONLY line that changed
- ☐ Task 3
// ...existing content...
```

This tells the edit_file tool: "Find this unique section and update ONLY these lines"

---

## Checklist Template for Each Update

After completing work, use this checklist BEFORE finishing:

- [ ] **Completed the implementation task** (code is functional, tested, builds successfully)
- [ ] **Used TARGETED EDITS** for IMPLEMENTATION_CHECKLIST.md (not full file replacement)
- [ ] **Used TARGETED EDITS** for MASTER_PROJECT_PLAN.md updates (not full file replacement)
- [ ] **Updated IMPLEMENTATION_CHECKLIST.md** with `✓` marks for completed tasks and `☐` for pending tasks
- [ ] **Updated MASTER_PROJECT_PLAN.md Quick Status Summary table** (adjust completed/pending counts)
- [ ] **Updated the corresponding Step in MASTER_PROJECT_PLAN.md** with:
  - [ ] Status changed to `completed` (or appropriate status)
  - [ ] Deliverables listed with `✓` marks for completed items and `☐` for pending items
  - [ ] Notes field includes what was accomplished and what's next
  - [ ] Dependencies noted if any failed
- [ ] **Code builds and tests pass** (`dotnet build && dotnet test`)
- [ ] **No uncommitted changes** left behind

---

## File Locations & Purposes

| File | Purpose | Update Frequency | Edit Strategy |
|------|---------|------------------|----------------|
| `/docs/IMPLEMENTATION_CHECKLIST.md` | Quick checklist of all tasks across all phases | After each task completion | **Targeted edits** (section by section) |
| `/docs/MASTER_PROJECT_PLAN.md` | Detailed persistent plan with status tracking for each step | After each phase step completion | **Targeted edits** (table + step sections) |
| `/docs/development/` | Setup guides (IDE, Database, Docker, Workflow) | When setup docs change | Full file (rarely changes) |
| `/CONTRIBUTING.md` | Contribution guidelines | When contribution process changes | Full file (rarely changes) |

---

## Editing Strategy Comparison

### ✅ GOOD: Targeted Edits (PREFERRED)
```
Use edit_file with:
- Specific section from file
- 5-10 lines of context on each side
- Only the lines that changed highlighted
- Clear explanation of what's changing
- Preserves file history and git diffs
```

### ⚠️ ACCEPTABLE: Full File Replacement (ONLY IF NECESSARY)
```
Use edit_file with:
- Entire file content
- Note: "Full file replacement because..."
- Only when targeted edits impossible
- Rare exception, not the norm
```

### ❌ WRONG: Multiple Separate Edits
```
DON'T do multiple edit_file calls for the same file:
1. Update status
2. Update deliverables  
3. Update notes

INSTEAD: Combine into a SINGLE targeted edit that changes all three
```

---

## Common Mistakes to Avoid

❌ **MISTAKE:** Completing code but forgetting to update MASTER_PROJECT_PLAN.md  
✅ **FIX:** Always update both files using targeted edits

❌ **MISTAKE:** Only updating IMPLEMENTATION_CHECKLIST.md  
✅ **FIX:** Update BOTH files for complete tracking using targeted edits

❌ **MISTAKE:** Replacing entire MASTER_PROJECT_PLAN.md file unnecessarily  
✅ **FIX:** Use targeted edits to preserve Git history (only full replacement as last resort)

❌ **MISTAKE:** Updating status without updating deliverables  
✅ **FIX:** Update Status AND Deliverables AND Notes together in ONE targeted edit

❌ **MISTAKE:** Using multiple edit_file calls for same file  
✅ **FIX:** Combine all changes into a single targeted edit

❌ **MISTAKE:** Providing entire file content to edit_file  
✅ **FIX:** Provide only the section being changed + minimal context lines

---

## Questions to Ask When Completing Work

Before marking a step as completed, answer:

1. **Is the code complete?** (All deliverables implemented and tested)
2. **Is IMPLEMENTATION_CHECKLIST.md updated?** (All relevant checkboxes marked `✓`)
   - ⭐ Did I use targeted edits? (Not full file replacement)
3. **Is MASTER_PROJECT_PLAN.md updated?** 
   - Quick Status Summary table? ✅ (targeted edit)
   - Step status changed to completed? ✅ (targeted edit)
   - Deliverables marked `✓`? ✅ (targeted edit)
   - Notes updated with what's next? ✅ (targeted edit)
4. **Do all tests pass?** (`dotnet test`)
5. **Does the code build?** (`dotnet build`)
6. **Are there any blocking issues?** (Document in Notes if yes)

---

## Example: Completing phase-0.1.1 (With Targeted Edits)

**Scenario:** Just finished implementing the Capability System Interfaces

**Step 1: Code is done, builds, tests pass** ✅

**Step 2: Update IMPLEMENTATION_CHECKLIST.md with TARGETED EDIT**

Find this section in the file:
```markdown
#### Capability System
- ☐ Create `ICapabilityInterface` marker interface
- ☐ Create `CapabilityTier` enum
- ☐ Implement public tier interfaces
- ☐ Implement restricted tier interfaces
- ☐ Implement privileged tier interfaces
```

Change to:
```markdown
#### Capability System
- ✓ Create `ICapabilityInterface` marker interface
- ✓ Create `CapabilityTier` enum
- ✓ Implement public tier interfaces
- ✓ Implement restricted tier interfaces
- ✓ Implement privileged tier interfaces
```

**Step 3: Update MASTER_PROJECT_PLAN.md Quick Status Summary with TARGETED EDIT**

Find this table row:
```markdown
| Phase | Steps | Completed | In Progress | Pending |
|-------|-------|-----------|-------------|
| Phase 0.1 | 11 | 3 | 0 | 8 |
```

Change to:
```markdown
| Phase | Steps | Completed | In Progress | Pending |
|-------|-------|-----------|-------------|
| Phase 0.1 | 11 | 4 | 0 | 7 |
```

**Step 4: Update MASTER_PROJECT_PLAN.md Step Details with TARGETED EDIT**

Find step phase-0.1.1:
```markdown
#### Step: phase-0.1.1 - Capability System Interfaces
**Status:** completed
**Deliverables:**
- ✓ `ICapabilityInterface` marker interface
- ✓ `CapabilityTier` enum (Public, Restricted, Privileged, Forbidden)
- ✓ Public tier interfaces (IUserDirectory, ICurrentUserContext, etc.)
- ✓ Restricted tier interfaces (IStorageProvider, IModuleSettings, ITeamDirectory)
- ✓ Privileged tier interfaces (IUserManager, IBackupProvider)

**Notes:** Capability system complete. Foundation for all authorization. Ready for phase-0.1.2.
```

**Step 5: Verify everything is updated** ✅

---

## Summary

**RULE OF THUMB:** If you've completed implementation work, you MUST:
1. ✅ Update IMPLEMENTATION_CHECKLIST.md using **TARGETED EDITS**
2. ✅ Update MASTER_PROJECT_PLAN.md using **TARGETED EDITS**
3. ✅ Update both Status, Deliverables, and Notes in one edit per section
4. ✅ Preserve Git history by avoiding full file replacements
5. ✅ **DELIVER COMPLETE WORK** - No half-measures, no "options"

**TARGETED EDITS are the professional, efficient, and preferred approach.**

**COMPLETE WORK is non-negotiable.**

---

## 🪟 Development Environment: Windows 11 with PowerShell

**CRITICAL:** This project is developed on **Windows 11**. All commands MUST use PowerShell syntax.

### PowerShell Commands (REQUIRED)

**ALWAYS use these PowerShell cmdlets:**
- ✅ `Get-Content` (NOT `cat`)
- ✅ `Get-ChildItem` (NOT `ls`)
- ✅ `Set-Location` (NOT `cd` when scripting)
- ✅ `Remove-Item` (NOT `rm`)
- ✅ `Copy-Item` (NOT `cp`)
- ✅ `Move-Item` (NOT `mv`)
- ✅ `New-Item` (NOT `touch` or `mkdir`)
- ✅ `Test-Path` (to check file existence)

### Path Conventions (REQUIRED)

**ALWAYS use Windows path format:**
- ✅ Backslashes: `src\Core\DotNetCloud.Core\`
- ✅ Windows-style: `D:\Repos\dotnetcloud\`
- ❌ NEVER use forward slashes for local paths (except in URLs)

### Cross-Platform .NET CLI (ALLOWED)

**These .NET commands work on Windows:**
- ✅ `dotnet build`
- ✅ `dotnet test`
- ✅ `dotnet run`
- ✅ `dotnet add package`
- ✅ `dotnet new`
- ✅ `dotnet restore`

### What NOT to Use

**NEVER use Linux/Bash commands:**
- ❌ `cat` → Use `Get-Content`
- ❌ `ls` → Use `Get-ChildItem`
- ❌ `rm` → Use `Remove-Item`
- ❌ `cp` → Use `Copy-Item`
- ❌ `mv` → Use `Move-Item`
- ❌ `touch` → Use `New-Item`
- ❌ `grep` → Use `Select-String`
- ❌ `find` → Use `Get-ChildItem -Recurse`

### Example: Correct PowerShell Usage

**✅ CORRECT:**
```powershell
Get-Content "src\Core\DotNetCloud.Core\README.md"
Get-ChildItem -Path "tests" -Recurse -Filter "*.csproj"
Remove-Item "bin\Debug" -Recurse -Force
dotnet build src\Core\DotNetCloud.Core\DotNetCloud.Core.csproj
```

**❌ WRONG:**
```bash
cat src/Core/DotNetCloud.Core/README.md
ls -R tests/*.csproj
rm -rf bin/Debug
dotnet build src/Core/DotNetCloud.Core/DotNetCloud.Core.csproj
```

### When User Says "remember"

When the user says the keyword **"remember"**, it means this information should be added to this instructions file for permanent reference across all future sessions.

### Mediator Command Execution Rule (MANDATORY)

When the assistant needs the mediator to run a command:
- Provide the exact command first.
- Stop and wait for the mediator to run that command.
- Do not continue with dependent steps until mediator output is received.

### Git Push Responsibility

- The assistant is responsible for pushing commits to remote by default.
- Do not delegate routine push responsibility to the moderator/user unless explicitly requested.

---

## Domain Information

**IMPORTANT:** The dotnetcloud.net domain has no content yet. References to dotnetcloud.net/docs are incorrect — documentation is only available from the GitHub repository (https://github.com/LLabmik/DotNetCloud). Only generic informational references to dotnetcloud.net are acceptable (website coming soon, contact, domain name). No URLs pointing to specific resources on dotnetcloud.net (docs, downloads, install scripts, etc.).

---

## Android Emulator Testing

### Local Testing Configuration

For local Android emulator testing in this workspace, use the DHCP-provided DNS servers:
- `192.168.0.14`
- `192.168.0.2`

**Note:** Avoid using custom proxy DNS settings for this purpose.

---

## Git Instruction Modification Protocol

If `.github/copilot-instructions.md` or any instruction/configuration file shows as modified in `git status`, NEVER restore or discard changes silently. Always run `git diff` on the file FIRST, show the user the full diff, and ask whether the changes should be kept or discarded. The user must make the decision about instruction file modifications.

---

## Android MAUI App Work

**IMPORTANT:** On monolith (Windows 11), only do Android MAUI app work. Server-side code changes must be handed off to mint22 via the CLIENT_SERVER_MEDIATION_HANDOFF.md document. Respect the role separation defined in the Environment table of the handoff document.
