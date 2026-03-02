# Copilot Instructions

## 🎯 CRITICAL: Dual Documentation Update Requirement

**⚠️ IMPORTANT:** After EVERY completed implementation task or phase step, you MUST update BOTH documentation files below. Failure to do so will result in incomplete project tracking. This is non-negotiable.

**💡 REMEMBER:** Use TARGETED EDITS (not full file replacement) to preserve Git history!

---

## Project Guidelines

### 1️⃣ Update IMPLEMENTATION_CHECKLIST.md (ALWAYS)

**When:** After completing any Phase 0-9 task or Pre-Implementation step  
**What:** Mark the corresponding checkbox as `[x]` in `/docs/IMPLEMENTATION_CHECKLIST.md`  
**How:** ⭐ **Use targeted edits (edit_file tool) with minimal context** — do NOT replace the entire file  
**Why:** Provides quick visibility into phase completion status for all stakeholders

**Example (GOOD - Targeted Edit):**
```markdown
### User Authentication
- [x] Implement user registration endpoint  ← Mark as completed
- [ ] Implement password reset flow         ← Still pending
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
  - `**Deliverables:**` list with [x] marks for completed items
  - `**Notes:**` with current progress and what comes next

**How:** ⭐ **Use targeted edits (edit_file tool) with minimal context FIRST** — only replace entire file as last resort  
**Why:** Preserves Git history, cleaner diffs, easier to review what changed

**Example (GOOD - Targeted Edit):**

Before:
```markdown
| Phase | Steps | Completed | In Progress | Pending |
|-------|-------|-----------|-------------|---------|
| Phase 0.1 | 11 | 3 | 0 | 8 |
```

After (targeted edit):
```markdown
| Phase | Steps | Completed | In Progress | Pending |
|-------|-------|-----------|-------------|---------|
| Phase 0.1 | 11 | 7 | 0 | 4 |
```

And update step section (targeted edit):
```markdown
#### Step: phase-0.1.4 - Event System Interfaces
**Status:** completed ✅
**Duration:** ~1.5 hours
**Deliverables:**
- [x] `IEvent` base interface
- [x] `IEventHandler<TEvent>` interface with `Task HandleAsync()` method
- [x] `IEventBus` interface with PublishAsync, SubscribeAsync, UnsubscribeAsync
- [x] Event subscription model

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
- [ ] Task 1
- [x] Task 2  ← This is the ONLY line that changed
- [ ] Task 3
// ...existing content...
```

This tells the edit_file tool: "Find this unique section and update ONLY these lines"

---

## Checklist Template for Each Update

After completing work, use this checklist BEFORE finishing:

- [ ] **Completed the implementation task** (code is functional, tested, builds successfully)
- [ ] **Used TARGETED EDITS** for IMPLEMENTATION_CHECKLIST.md (not full file replacement)
- [ ] **Used TARGETED EDITS** for MASTER_PROJECT_PLAN.md updates (not full file replacement)
- [ ] **Updated IMPLEMENTATION_CHECKLIST.md** with [x] marks for completed tasks
- [ ] **Updated MASTER_PROJECT_PLAN.md Quick Status Summary table** (adjust completed/pending counts)
- [ ] **Updated the corresponding Step in MASTER_PROJECT_PLAN.md** with:
  - [ ] Status changed to `completed` (or appropriate status)
  - [ ] Deliverables listed with [x] marks
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
2. **Is IMPLEMENTATION_CHECKLIST.md updated?** (All relevant checkboxes marked [x])
   - ⭐ Did I use targeted edits? (Not full file replacement)
3. **Is MASTER_PROJECT_PLAN.md updated?** 
   - Quick Status Summary table? ✅ (targeted edit)
   - Step status changed to completed? ✅ (targeted edit)
   - Deliverables marked [x]? ✅ (targeted edit)
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
- [ ] Create `ICapabilityInterface` marker interface
- [ ] Create `CapabilityTier` enum
- [ ] Implement public tier interfaces
- [ ] Implement restricted tier interfaces
- [ ] Implement privileged tier interfaces
```

Change to:
```markdown
#### Capability System
- [x] Create `ICapabilityInterface` marker interface
- [x] Create `CapabilityTier` enum
- [x] Implement public tier interfaces
- [x] Implement restricted tier interfaces
- [x] Implement privileged tier interfaces
```

**Step 3: Update MASTER_PROJECT_PLAN.md Quick Status Summary with TARGETED EDIT**

Find this table row:
```markdown
| Phase 0.1 | 11 | 3 | 0 | 8 |
```

Change to:
```markdown
| Phase 0.1 | 11 | 4 | 0 | 7 |
```

**Step 4: Update MASTER_PROJECT_PLAN.md Step Details with TARGETED EDIT**

Find step phase-0.1.1:
```markdown
#### Step: phase-0.1.1 - Capability System Interfaces
**Status:** completed
**Deliverables:**
- [x] `ICapabilityInterface` marker interface
- [x] `CapabilityTier` enum (Public, Restricted, Privileged, Forbidden)
- [x] Public tier interfaces (IUserDirectory, ICurrentUserContext, etc.)
- [x] Restricted tier interfaces (IStorageProvider, IModuleSettings, ITeamDirectory)
- [x] Privileged tier interfaces (IUserManager, IBackupProvider)

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

**TARGETED EDITS are the professional, efficient, and preferred approach.**