#!/usr/bin/env python3
"""Fix mangled StartPokerForCurrentCardAsync calls in test files."""
import re
import os

TEST_DIR = "/home/benk/Repos/dotnetcloud/tests/DotNetCloud.Modules.Tracks.Tests"

def fix_file(filepath):
    with open(filepath, 'r') as f:
        content = f.read()

    original = content

    # Pattern 1: Direct poker start calls
    # var xxx = await _reviewService.StartReviewSessionAsync,
    #     new CreatePokerSessionDto { ... }, _admin);
    # →
    # var xxx = await _pokerService.StartSessionAsync(_teamBoard.Id, _admin,
    #     new CreatePokerSessionDto { ... });
    #
    # Also for _service.StartReviewSessionAsync (in ReviewSessionServiceTests)

    # Multi-line pattern: method call followed by comma (instead of paren),
    # then CreatePokerSessionDto on next line, then closing
    pattern1 = re.compile(
        r'(var \w+) = await (_reviewService|_service)\.StartReviewSessionAsync,\n'
        r'(\s+new CreatePokerSessionDto\s*\{[^}]*\}), (_admin|_owner|_member|_viewer|_caller)\);',
        re.MULTILINE
    )

    def fix_direct_call(m):
        var_name = m.group(1)
        service_var = m.group(2)
        dto_line = m.group(3)
        caller = m.group(4)

        # Determine the epic/product variable. In most cases it's _teamBoard.Id
        # But in ReviewSessionServiceTests it might be _product.Id or similar
        if 'ReviewSessionServiceTests' in filepath:
            epic_id = '_product.Id'
        else:
            epic_id = '_teamBoard.Id'

        return f'{var_name} = await _pokerService.StartSessionAsync({epic_id}, {caller},\n        {dto_line});'

    content = pattern1.sub(fix_direct_call, content)

    # Pattern 2: Single-line poker start
    # await _reviewService.StartReviewSessionAsync, new CreatePokerSessionDto(), _owner);
    pattern2 = re.compile(
        r'await (_reviewService|_service)\.StartReviewSessionAsync, (new CreatePokerSessionDto\(\)), (_admin|_owner|_member|_viewer|_caller)\);'
    )

    def fix_single_line(m):
        service_var = m.group(1)
        dto = m.group(2)
        caller = m.group(3)
        if 'ReviewSessionServiceTests' in filepath:
            epic_id = '_product.Id'
        else:
            epic_id = '_teamBoard.Id'
        return f'await _pokerService.StartSessionAsync({epic_id}, {caller}, {dto});'

    content = pattern2.sub(fix_single_line, content)

    # Pattern 3: Assert.ThrowsExactlyAsync lambda
    # () => _reviewService.StartReviewSessionAsync, new CreatePokerSessionDto(), _member));
    # →
    # () => _pokerService.StartSessionAsync(_teamBoard.Id, _member, new CreatePokerSessionDto()));
    pattern3 = re.compile(
        r'\(\) => (_reviewService|_service)\.StartReviewSessionAsync, (new CreatePokerSessionDto\(\)), (_admin|_owner|_member|_viewer|_caller)\)\);'
    )

    def fix_throws(m):
        service_var = m.group(1)
        dto = m.group(2)
        caller = m.group(3)
        if 'ReviewSessionServiceTests' in filepath:
            epic_id = '_product.Id'
        else:
            epic_id = '_teamBoard.Id'
        return f'() => _pokerService.StartSessionAsync({epic_id}, {caller}, {dto}));'

    content = pattern3.sub(fix_throws, content)

    # Pattern 4: Fix remaining StartReviewSessionAsync, → StartSessionAsync(
    # (single-arg cases that weren't caught above)
    content = content.replace('_reviewService.StartReviewSessionAsync,', '_reviewService.StartReviewSessionAsync(')
    content = content.replace('_service.StartReviewSessionAsync,', '_service.StartReviewSessionAsync(')

    # Pattern 5: Fix ActivePokerSession references
    # withPoker.ActivePokerSession → withPoker (the result is now the poker session directly)
    # But only in lines that were fixed above (we replaced the method call)
    # This is hard to do precisely. Let's do it broadly.
    content = content.replace('.ActivePokerSession!', '!')
    content = content.replace('.ActivePokerSession.', '.')
    content = content.replace('.ActivePokerSession;', ';')
    content = content.replace('.ActivePokerSession)', ')')

    # Pattern 6: Fix dangling ) where the original call had trailing )
    # e.g., ...CreatePokerSessionDto { ... }), _caller);  (extra paren)
    # These should already be handled by patterns 1-3

    # Pattern 7: Fix missing CancellationToken.None in some calls
    # The StartSessionAsync needs a CancellationToken as last arg...
    # Actually, looking at the PokerService, it might have overloads.
    # Let's just handle this if it's an issue after building.

    if content != original:
        with open(filepath, 'w') as f:
            f.write(content)
        return True
    return False

def main():
    files = [
        'PhaseI_LiveReviewModeTests.cs',
        'PhaseJ_ComprehensiveTests.cs',
        'ReviewSessionServiceTests.cs',
    ]
    for fn in files:
        fp = os.path.join(TEST_DIR, fn)
        if fix_file(fp):
            print(f'Fixed {fn}')
        else:
            print(f'No changes to {fn}')

if __name__ == '__main__':
    main()
