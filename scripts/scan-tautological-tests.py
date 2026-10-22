#!/usr/bin/env python3
"""Heuristic, method-level scan for tautological-only tests.

This is the Phase 1 "fast pass" from docs/TEST_TAUTOLOGY_AUDIT_PLAN.md. It is
deliberately conservative: it only reports the unambiguous shapes (T1-T4) plus
the weak-but-valid buckets (W1/W2) so the candidate pool can be sized before
the authoritative Roslyn scanner is written.

Usage:
    python3 scripts/scan-tautological-tests.py [--tests-root tests] [--out artifacts/test-tautology]

Outputs:
    <out>/findings-heuristic.csv
    <out>/findings-heuristic.json
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import re
import sys
from dataclasses import dataclass, field, asdict
from pathlib import Path

TEST_ATTRS = ("TestMethod", "DataTestMethod", "Theory", "Fact")
ASSERT_RE = re.compile(r"\bAssert\.\w+|\bCollectionAssert\.\w+|\bStringAssert\.\w+|\.Should\s*\(")
ASSERT_HELPER_RE = re.compile(r"\bAssert[A-Za-z_]\w*\s*\(")
THROWS_RE = re.compile(r"\bAssert\.Throws|\[ExpectedException|Record\.Exception")
VERIFY_RE = re.compile(r"\.Verify\s*\(|\.VerifyNoOtherCalls\s*\(|\.Received\s*\(")
MOCK_SETUP_RE = re.compile(r"\.Setup\s*\(|\.SetupSequence\s*\(|new\s+Mock<|Mock\.Of<|MockBehavior")
CALL_RE = re.compile(r"(?<![\w])(?<!new\s)(?:await\s+)?([A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*)\s*\(")
NOT_PRODUCTION_CALLEE = {
    "Assert", "CollectionAssert", "StringAssert", "Mock", "It", "Times", "Verify",
    "Setup", "SetupSequence", "Returns", "ReturnsAsync", "Throws", "ThrowsAsync",
    "Callback", "nameof", "typeof", "new", "if", "for", "foreach", "while", "switch",
    "using", "lock", "catch", "return", "await", "TestHelpers", "Should", "Shouldly",
    "Of", "ToString",
}


@dataclass
class TestMethod:
    file: str
    line: int
    name: str
    body: str
    raw_body: str = ""
    flags: list[str] = field(default_factory=list)
    assertion_kinds: dict[str, int] = field(default_factory=dict)

    @property
    def tautological(self) -> bool:
        return bool({"T1", "T2", "T3", "T4"} & set(self.flags))

    @property
    def weak_only(self) -> bool:
        return not self.tautological and bool({"W1", "W2", "W3"} & set(self.flags))


def strip_comments_and_strings(source: str) -> str:
    """Blank out comments and string/char literals, preserving length and newlines.

    Handles regular, verbatim (``@"..."``), interpolated and C# 11 raw (``\"\"\"``)
    strings. Raw-string handling matters here because test fixtures embed JSON
    containing braces, which would otherwise corrupt brace matching.
    """
    out: list[str] = list(source)
    n = len(source)

    def blank(start: int, end: int) -> None:
        for k in range(start, min(end, n)):
            if out[k] != "\n":
                out[k] = " "

    i = 0
    while i < n:
        c = source[i]
        nxt = source[i + 1] if i + 1 < n else ""

        if c == "/" and nxt == "/":
            end = source.find("\n", i)
            end = n if end == -1 else end
            blank(i, end)
            i = end
            continue

        if c == "/" and nxt == "*":
            end = source.find("*/", i + 2)
            end = n if end == -1 else end + 2
            blank(i, end)
            i = end
            continue

        if c == '"':
            run = 1
            while i + run < n and source[i + run] == '"':
                run += 1
            if run >= 3:  # raw string literal
                close = source.find('"' * run, i + run)
                end = n if close == -1 else close + run
                blank(i, end)
                i = end
                continue

            verbatim = i > 0 and source[i - 1] == "@"
            j = i + 1
            while j < n:
                if source[j] == '"':
                    if verbatim and j + 1 < n and source[j + 1] == '"':
                        j += 2
                        continue
                    break
                if not verbatim and source[j] == "\\":
                    j += 2
                    continue
                if source[j] == "\n" and not verbatim:
                    break
                j += 1
            end = min(n, j + 1)
            blank(i, end)
            i = end
            continue

        if c == "'":
            j = i + 1
            while j < n:
                if source[j] == "\\":
                    j += 2
                    continue
                if source[j] == "'":
                    break
                j += 1
            end = min(n, j + 1)
            blank(i, end)
            i = end
            continue

        i += 1

    return "".join(out)


def find_method_body(cleaned: str, start: int) -> tuple[str, int, int, int] | None:
    """Find the body of the method after `start`.

    Supports both block bodies and expression-bodied methods (``=> ...;``).
    Returns (body, body_start, body_end, header_end). Offsets are valid for the
    raw source too, because stripping preserves character length and newlines.
    """
    brace_idx = cleaned.find("{", start)
    arrow_idx = cleaned.find("=>", start)

    if arrow_idx != -1 and (brace_idx == -1 or arrow_idx < brace_idx):
        depth = 0
        i = arrow_idx + 2
        while i < len(cleaned):
            ch = cleaned[i]
            if ch in "([{":
                depth += 1
            elif ch in ")]}":
                depth -= 1
            elif ch == ";" and depth == 0:
                return cleaned[arrow_idx + 2 : i], arrow_idx + 2, i, arrow_idx
            i += 1
        return None

    if brace_idx == -1:
        return None
    depth = 0
    for i in range(brace_idx, len(cleaned)):
        if cleaned[i] == "{":
            depth += 1
        elif cleaned[i] == "}":
            depth -= 1
            if depth == 0:
                return cleaned[brace_idx + 1 : i], brace_idx + 1, i, brace_idx
    return None


def extract_tests(path: Path, repo_root: Path) -> list[TestMethod]:
    raw = path.read_text(encoding="utf-8", errors="replace")
    cleaned = strip_comments_and_strings(raw)
    tests: list[TestMethod] = []
    attr_re = re.compile(r"\[\s*(?:" + "|".join(TEST_ATTRS) + r")\b")
    for match in attr_re.finditer(cleaned):
        found = find_method_body(cleaned, match.end())
        if not found:
            continue
        body, body_start, body_end, header_end = found
        line = cleaned.count("\n", 0, match.start()) + 1
        # Method name: nearest identifier followed by '(' before the body.
        header = cleaned[match.end() : header_end]
        name_match = re.findall(r"([A-Za-z_]\w*)\s*\(", header)
        name = name_match[-1] if name_match else "<unknown>"
        tests.append(
            TestMethod(
                file=str(path.relative_to(repo_root)),
                line=line,
                name=name,
                body=body,
                # Includes attributes + signature so DataRow variants are distinct.
                raw_body=raw[match.start() : body_end],
            )
        )
    return tests


def count(pattern: re.Pattern[str], text: str) -> int:
    return len(pattern.findall(text))


def has_production_call(body: str) -> bool:
    """Approximate whether the body invokes something other than test/mock plumbing."""
    scrubbed = re.sub(
        r"\.\s*(?:Setup|SetupSequence|Returns|ReturnsAsync|Callback|Throws|ThrowsAsync|Verify|VerifyNoOtherCalls|Received)\s*\(",
        "(",
        body,
    )
    for match in CALL_RE.finditer(scrubbed):
        callee = match.group(1).split(".")[-1].strip()
        if callee in NOT_PRODUCTION_CALLEE or callee.startswith("Assert"):
            continue
        # Helper-looking calls are deliberately NOT excluded: counting them as
        # production only pushes a no-assert test into W3, which is the safe side.
        return True
    return False


def classify(test: TestMethod) -> None:
    body = test.body
    asserts = ASSERT_RE.findall(body)
    helper_asserts = ASSERT_HELPER_RE.findall(body)
    throws = THROWS_RE.findall(body)
    verifies = VERIFY_RE.findall(body)
    setups = MOCK_SETUP_RE.findall(body)

    test.assertion_kinds = {
        "asserts": len(asserts),
        "helper_asserts": len(helper_asserts),
        "throws": len(throws),
        "verify": len(verifies),
        "mock_setup": len(setups),
    }

    has_assertion = bool(asserts or helper_asserts or throws or verifies)

    if not has_assertion:
        # A manual try/catch that swallows the exception and asserts nothing is a
        # classic tautology: the test throws and catches its own exception.
        if re.search(r"\bcatch\b", body):
            test.flags.append("T1")
            return
        # W3: implicitly asserts "does not throw" because production code runs.
        # T1: no assertion and no production call at all (pure setup / no-op).
        test.flags.append("W3" if has_production_call(body) else "T1")
        return

    # T3: literal/constant truth.
    if re.search(r"\bAssert\.(?:IsTrue|IsFalse)\s*\(\s*(?:true|false)\s*\)", body):
        test.flags.append("T3")
    if re.search(r"\bAssert\.AreEqual\s*\(\s*(\"[^\"]*\"|[-\d.]+)\s*,\s*\1\s*\)", body):
        test.flags.append("T3")

    # T2: identical-argument assertions.
    if re.search(r"\bAssert\.\w+\s*\(\s*([A-Za-z_]\w*)\s*,\s*\1\s*[,)]", body):
        test.flags.append("T2")

    # T4: non-null/type check on an object constructed in the test.
    if re.search(r"\bAssert\.(?:IsNotNull|IsInstanceOfType|IsAssignableFrom)\s*\(\s*new\s+[A-Za-z_]", body):
        test.flags.append("T4")

    # W1: only a non-null check, no value/state assertion.
    only_not_null = asserts and all(
        re.match(r"Assert\.(?:IsNotNull|IsInstanceOfType|IsAssignableFrom)$", a) for a in asserts
    ) and not throws and not verifies
    if only_not_null:
        test.flags.append("W1")

    # W2: only mock interaction checks.
    if verifies and not asserts and not throws:
        test.flags.append("W2")

    # T6/T7 (mock round-trip / test-owned fixture asserts) are left to the
    # authoritative Roslyn scanner - textual heuristics here are too noisy.


def normalized_hash(body: str) -> str:
    """Hash the RAW body so tests that differ only by string literals are not merged."""
    normalized = re.sub(r"\s+", " ", body).strip()
    return hashlib.sha256(normalized.encode("utf-8")).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--tests-root", default="tests")
    parser.add_argument("--out", default="artifacts/test-tautology")
    parser.add_argument("--repo-root", default=".")
    args = parser.parse_args()

    repo_root = Path(args.repo_root).resolve()
    tests_root = (repo_root / args.tests_root).resolve()
    out_dir = (repo_root / args.out).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    files = []
    for path in tests_root.rglob("*Tests.cs"):
        parts = set(path.parts)
        if "bin" in parts or "obj" in parts:
            continue
        files.append(path)
    files.sort()

    all_tests: list[TestMethod] = []
    for path in files:
        all_tests.extend(extract_tests(path, repo_root))

    for t in all_tests:
        classify(t)

    # Duplicate bodies (T8) - exact normalised match within the SAME file/class.
    # Cross-service lookalike tests are legitimate parallel coverage, not duplicates.
    by_project: dict[str, dict[str, list[TestMethod]]] = {}
    for t in all_tests:
        project = Path(t.file).parts[1] if len(Path(t.file).parts) > 1 else "?"
        by_project.setdefault(project, {}).setdefault(t.file, []).append(t)
    for project, per_file in by_project.items():
        for file_tests in per_file.values():
            groups: dict[str, list[TestMethod]] = {}
            for t in file_tests:
                groups.setdefault(normalized_hash(t.raw_body), []).append(t)
            for group in groups.values():
                if len(group) > 1:
                    for t in group:
                        t.flags.append("T8")

    tautological = [t for t in all_tests if t.tautological]
    weak = [t for t in all_tests if t.weak_only and "T8" not in t.flags]
    duplicates = [t for t in all_tests if "T8" in t.flags]

    csv_path = out_dir / "findings-heuristic.csv"
    with csv_path.open("w", newline="", encoding="utf-8") as fh:
        writer = csv.writer(fh)
        writer.writerow(["file", "line", "method", "flags", "asserts", "helper_asserts", "throws", "verify", "mock_setup"])
        for t in all_tests:
            if not t.flags:
                continue
            writer.writerow([
                t.file, t.line, t.name, ";".join(sorted(set(t.flags))),
                t.assertion_kinds.get("asserts", 0),
                t.assertion_kinds.get("helper_asserts", 0),
                t.assertion_kinds.get("throws", 0),
                t.assertion_kinds.get("verify", 0),
                t.assertion_kinds.get("mock_setup", 0),
            ])

    json_path = out_dir / "findings-heuristic.json"
    json_path.write_text(
        json.dumps([asdict(t) for t in all_tests if t.flags], indent=2),
        encoding="utf-8",
    )

    # Summary
    print(f"Scanned {len(files)} test files, {len(all_tests)} test methods")
    print(f"Tautological-only candidates (T1-T4): {len(tautological)}")
    print(f"Weak-but-valid (W1-W3):               {len(weak)}")
    print(f"Duplicate bodies (T8):                {len(duplicates)}")
    print()
    print(f"{'Project':45} {'Tests':>6} {'Taut':>6} {'Weak':>6} {'T8':>5}")
    print("-" * 74)
    rows = []
    for project, per_file in sorted(by_project.items()):
        ts = [t for g in per_file.values() for t in g]
        rows.append((
            project,
            len(ts),
            sum(1 for t in ts if t.tautological),
            sum(1 for t in ts if t.weak_only and "T8" not in t.flags),
            sum(1 for t in ts if "T8" in t.flags),
        ))
    for row in sorted(rows, key=lambda r: (-r[2], -r[1])):
        print(f"{row[0]:45} {row[1]:>6} {row[2]:>6} {row[3]:>6} {row[4]:>5}")
    print()
    print(f"CSV:  {csv_path}")
    print(f"JSON: {json_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
