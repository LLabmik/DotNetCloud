#!/usr/bin/env python3
"""Fix SprintPlanningService API changes in test files."""

import re
import sys

FILES = [
    "tests/DotNetCloud.Modules.Tracks.Tests/SprintPlanningServiceTests.cs",
    "tests/DotNetCloud.Modules.Tracks.Tests/PhaseJ_ComprehensiveTests.cs",
    "tests/DotNetCloud.Modules.Tracks.Tests/TracksPerformanceTests.cs",
]

def apply_fixes(filepath):
    with open(filepath, 'r') as f:
        content = f.read()

    original = content

    # 1. DTO property renames (CreateSprintPlanDto only)
    content = content.replace("SprintCount =", "NumberOfSprints =")
    content = content.replace("DefaultDurationWeeks =", "SprintDurationWeeks =")

    # 2. Type rename
    content = content.replace("SprintPlanOverviewDto", "List<SprintDto>")

    # 3. Method renames
    content = content.replace("AdjustSprintAsync(", "AdjustSprintDatesAsync(")
    content = content.replace("GetPlanOverviewAsync(", "GetSprintPlanAsync(")

    # 4. Remove .Sprints property access (result.Sprints -> result, plan.Sprints -> plan, etc.)
    #   "result.Sprints." -> "result."
    #   "result.Sprints[" -> "result["
    #   "plan.Sprints." -> "plan."
    #   "plan.Sprints[" -> "plan["
    #   "overview.Sprints." -> "overview."
    #   "allSprints = result.Sprints" -> "allSprints = result"
    content = re.sub(r'\.Sprints\.', '.', content)
    content = re.sub(r'\.Sprints\[', '[', content)
    content = re.sub(r'= \w+\.Sprints;', lambda m: m.group(0).replace('.Sprints;', ';'), content)
    # Also standalone ".Sprints;" in variable assignments
    content = re.sub(r'(\w+)\.Sprints', r'\1', content)

    # 5. Fix CallerContext args to CancellationToken.None in service calls
    #    _service.Method(board.Id, dto, _admin) -> _service.Method(board.Id, dto, CancellationToken.None)
    #    _planningService.Method(board.Id, dto, _owner) -> _planningService.Method(board.Id, dto, CancellationToken.None)
    #    planningService.Method(board.Id, dto, _caller) -> planningService.Method(board.Id, dto, CancellationToken.None)
    ctx_names = ['_admin', '_member', '_owner', '_caller', '_viewer', '_outsider', '_memberUserId',
                 '_adminUserId', 'outsider']
    for ctx in ctx_names:
        # Replace ")  at end of CreateSprintPlanAsync/AdjustSprintDatesAsync/GetSprintPlanAsync calls
        content = re.sub(
            rf'(CreateSprintPlanAsync|AdjustSprintDatesAsync|GetSprintPlanAsync)\(([^)]*),\s*{ctx}\s*\)',
            r'\1(\2, CancellationToken.None)',
            content
        )

    # 6. Fix SprintPlanOverviewDto in method parameter types (if any remain)
    # Already handled by #2

    # 7. Fix .ProductId -> _board.Id or board.Id (context-dependent)
    #    We'll handle this manually after build

    # 8. Fix .TotalWeeks -> .Sum(s => (s.DurationWeeks ?? 0))
    #    We'll handle this manually after build

    # 9. Fix .PlanStartDate, .PlanEndDate
    #    We'll handle this manually after build

    if content != original:
        with open(filepath, 'w') as f:
            f.write(content)
        print(f"✓ {filepath}")
    else:
        print(f"  {filepath} (no changes)")

for f in FILES:
    apply_fixes(f)
