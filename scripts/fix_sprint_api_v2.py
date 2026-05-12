#!/usr/bin/env python3
"""Precise SprintPlanningService API fixes for test files."""
import sys

FILES = [
    "tests/DotNetCloud.Modules.Tracks.Tests/SprintPlanningServiceTests.cs",
    "tests/DotNetCloud.Modules.Tracks.Tests/PhaseJ_ComprehensiveTests.cs",
    "tests/DotNetCloud.Modules.Tracks.Tests/TracksPerformanceTests.cs",
]

for filepath in FILES:
    with open(filepath, 'r') as f:
        content = f.read()

    original = content

    # 1. DTO property renames
    content = content.replace("SprintCount =", "NumberOfSprints =")
    content = content.replace("DefaultDurationWeeks =", "SprintDurationWeeks =")

    # 2. Return type rename
    content = content.replace("SprintPlanOverviewDto", "List<SprintDto>")

    # 3. Method renames
    content = content.replace("AdjustSprintAsync(", "AdjustSprintDatesAsync(")
    content = content.replace("GetPlanOverviewAsync(", "GetSprintPlanAsync(")

    # 4. Remove .Sprints property access — ONLY for known local variables
    #    result.Sprints -> result
    #    plan.Sprints -> plan
    #    overview.Sprints -> overview
    for var in ['result', 'plan', 'overview', 'allSprints']:
        # variable.Sprints[ -> variable[
        content = content.replace(f'{var}.Sprints[', f'{var}[')
        # variable.Sprints. -> variable.
        content = content.replace(f'{var}.Sprints.', f'{var}.')
        # variable.Sprints; -> variable;
        content = content.replace(f'{var}.Sprints;', f'{var};')
        # variable.Sprints) -> variable)
        content = content.replace(f'{var}.Sprints)', f'{var})')
        # = variable.Sprints (as complete expression ending with newline/comma)
        # Handled by the above patterns in most cases

    # 5. Convert CallerContext params to CancellationToken.None in sprint service calls
    ctx_names = ['_admin', '_member', '_owner', '_caller', '_viewer', '_outsider', 'outsider',
                 '_memberUserId', '_adminUserId']
    for ctx in ctx_names:
        old = f', {ctx})'
        new = f', CancellationToken.None)'
        content = content.replace(old, new)
        # Also handle double-close paren from nested calls
        # e.g. () => _service.Method(x, y, _admin))
        # becomes () => _service.Method(x, y, CancellationToken.None))

    # 6. Fix ProductId -> board.Id (context-dependent)
    #    In SprintPlanningServiceTests: result.ProductId -> _board.Id
    #    In PhaseJ: result.ProductId -> board.Id (the local var)
    #    We'll handle these manually since context varies

    # 7. Fix TotalWeeks, PlanStartDate, PlanEndDate -> will handle manually

    if content != original:
        with open(filepath, 'w') as f:
            f.write(content)
        print(f"✓ {filepath}")
    else:
        print(f"  {filepath} (no changes)")

print("Done. Manual fixes needed for: ProductId, TotalWeeks, PlanStartDate, PlanEndDate")
