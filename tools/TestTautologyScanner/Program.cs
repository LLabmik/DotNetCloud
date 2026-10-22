using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestTautologyScanner;

/// <summary>
/// Authoritative, Roslyn-based classification of tautological-only tests.
/// See docs/TEST_TAUTOLOGY_AUDIT_PLAN.md (Phase 1).
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var repoRoot = Directory.GetCurrentDirectory();
        var testsRoot = Path.Combine(repoRoot, "tests");
        var outDir = Path.Combine(repoRoot, "artifacts", "test-tautology");

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--tests-root" when i + 1 < args.Length:
                    testsRoot = Path.GetFullPath(args[++i]);
                    break;
                case "--out" when i + 1 < args.Length:
                    outDir = Path.GetFullPath(args[++i]);
                    break;
                case "--repo-root" when i + 1 < args.Length:
                    repoRoot = Path.GetFullPath(args[++i]);
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    return 2;
            }
        }

        if (!Directory.Exists(testsRoot))
        {
            Console.Error.WriteLine($"Tests root not found: {testsRoot}");
            return 2;
        }

        Directory.CreateDirectory(outDir);

        var scanner = new TestScanner(repoRoot);
        var result = scanner.Scan(testsRoot);
        result.Write(outDir);
        result.PrintSummary();
        return 0;
    }
}

internal sealed record AssertionCounts(int Assertions, int Throws, int Verifies, int ProductionCalls);

internal sealed record MethodFinding(
    string File,
    int Line,
    string ClassName,
    string MethodName,
    IReadOnlyList<string> Flags,
    AssertionCounts Counts,
    string Evidence)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasFlags => Flags.Count > 0;

    [System.Text.Json.Serialization.JsonIgnore]
    public string Classification => Flags.Count == 0 ? "clean" : string.Join(';', Flags);
}

internal sealed record ScanResult(IReadOnlyList<MethodFinding> Findings, int FilesScanned)
{
    private static readonly string[] FlagOrder = ["T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "W1", "W2", "W3"];
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public void Write(string outDir)
    {
        var jsonPath = Path.Combine(outDir, "findings-roslyn.json");
        File.WriteAllText(
            jsonPath,
            JsonSerializer.Serialize(Findings, new JsonSerializerOptions { WriteIndented = true }),
            Utf8NoBom);

        var csvPath = Path.Combine(outDir, "findings-roslyn.csv");
        var csv = new StringBuilder();
        csv.AppendLine("file,line,class,method,flags,assertions,throws,verifies,production_calls,evidence");
        foreach (var finding in Findings.Where(f => f.HasFlags))
        {
            csv.AppendLine(string.Join(
                ',',
                Csv(finding.File),
                finding.Line,
                Csv(finding.ClassName),
                Csv(finding.MethodName),
                Csv(finding.Classification),
                finding.Counts.Assertions,
                finding.Counts.Throws,
                finding.Counts.Verifies,
                finding.Counts.ProductionCalls,
                Csv(finding.Evidence)));
        }

        File.WriteAllText(csvPath, csv.ToString(), Utf8NoBom);
        Console.WriteLine($"CSV:  {csvPath}");
        Console.WriteLine($"JSON: {jsonPath}");
    }

    public void PrintSummary()
    {
        Console.WriteLine($"Scanned {FilesScanned} files, {Findings.Count} test methods");
        Console.WriteLine();

        foreach (var flag in FlagOrder)
        {
            var count = Findings.Count(f => f.Flags.Contains(flag));
            if (count > 0)
            {
                Console.WriteLine($"{flag,-3} {count,5}");
            }
        }

        var strict = Findings.Count(f => f.Flags.Any(x => x.StartsWith('T')));
        var weak = Findings.Count(f => f.Flags.Any(x => x.StartsWith('W')));
        Console.WriteLine();
        Console.WriteLine($"Strict tautological candidates (T1-T8): {strict}");
        Console.WriteLine($"Weak-but-valid (W1-W3):                 {weak}");
        Console.WriteLine();
        Console.WriteLine($"{"Project",-45}{"Tests",6}{"Flagged",8}{"T",6}{"W",6}");
        Console.WriteLine(new string('-', 71));

        var rows = Findings
            .GroupBy(f => ProjectOf(f.File))
            .Select(g => new
            {
                Project = g.Key,
                Tests = g.Count(),
                Flagged = g.Count(f => f.HasFlags),
                T = g.Count(f => f.Flags.Any(x => x.StartsWith('T'))),
                W = g.Count(f => f.Flags.Any(x => x.StartsWith('W'))),
            })
            .OrderByDescending(r => r.T)
            .ThenByDescending(r => r.Tests);

        foreach (var row in rows)
        {
            Console.WriteLine($"{row.Project,-45}{row.Tests,6}{row.Flagged,8}{row.T,6}{row.W,6}");
        }
    }

    private static string ProjectOf(string file)
    {
        var parts = file.Replace('\\', '/').Split('/');
        var index = Array.IndexOf(parts, "tests");
        return index >= 0 && index + 1 < parts.Length ? parts[index + 1] : "?";
    }

    private static string Csv(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
}

internal sealed class TestScanner
{
    private static readonly HashSet<string> TestAttributes = new(StringComparer.Ordinal)
    {
        "TestMethod", "DataTestMethod", "Theory", "Fact",
    };

    private static readonly HashSet<string> WeakAssertions = new(StringComparer.Ordinal)
    {
        "IsNotNull", "IsInstanceOfType", "IsAssignableFrom",
    };

    private static readonly HashSet<string> Plumbing = new(StringComparer.Ordinal)
    {
        "Setup", "SetupSequence", "Returns", "ReturnsAsync", "Callback", "Throws", "ThrowsAsync",
        "Verify", "VerifyNoOtherCalls", "VerifyGet", "VerifySet", "Of", "When", "Do",
        "ConfigureAwait", "GetAwaiter", "GetResult", "ToString", "nameof", "typeof",
    };

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private readonly string _repoRoot;

    public TestScanner(string repoRoot) => _repoRoot = repoRoot;

    public ScanResult Scan(string testsRoot)
    {
        var findings = new List<MethodFinding>();
        var files = Directory
            .EnumerateFiles(testsRoot, "*Tests.cs", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Preview));
            var root = tree.GetCompilationUnitRoot();
            var mockReturns = CollectMockReturns(root);
            var analyzed = new List<(MethodFinding Finding, string DuplicateKey)>();

            foreach (var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
                {
                    if (!HasTestAttribute(method))
                    {
                        continue;
                    }

                    analyzed.Add(Analyze(method, classDeclaration.Identifier.ValueText, file, mockReturns));
                }
            }

            // T8: identical attribute data + body within the same file.
            var duplicateKeys = analyzed
                .GroupBy(item => item.DuplicateKey, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var (finding, key) in analyzed)
            {
                findings.Add(duplicateKeys.Contains(key)
                    ? finding with
                    {
                        Flags = finding.Flags.Contains("T8", StringComparer.Ordinal)
                            ? finding.Flags
                            : [.. finding.Flags, "T8"],
                        Evidence = finding.Evidence.Length == 0
                            ? "duplicate body within file"
                            : finding.Evidence + " | duplicate body within file",
                    }
                    : finding);
            }
        }

        return new ScanResult(findings, files.Count);
    }

    private string RelativePath(string path)
        => Path.GetRelativePath(_repoRoot, path).Replace('\\', '/');

    private static bool IsSourceFile(string path)
    {
        var normalized = path.Replace('\\', '/');
        return !normalized.Contains("/bin/", StringComparison.Ordinal)
            && !normalized.Contains("/obj/", StringComparison.Ordinal);
    }

    private static bool HasTestAttribute(MethodDeclarationSyntax method)
        => method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(attribute => attribute.Name.ToString().Split('.').Last())
            .Any(TestAttributes.Contains);

    private (MethodFinding Finding, string DuplicateKey) Analyze(
        MethodDeclarationSyntax method,
        string className,
        string file,
        HashSet<string> mockReturns)
    {
        var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();
        var assertions = new List<InvocationExpressionSyntax>();
        var throws = new List<InvocationExpressionSyntax>();
        var verifies = new List<InvocationExpressionSyntax>();
        var production = new List<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (IsThrowsAssertion(invocation))
            {
                throws.Add(invocation);
            }
            else if (IsVerify(invocation))
            {
                verifies.Add(invocation);
            }
            else if (IsAssertion(invocation))
            {
                assertions.Add(invocation);
            }
            else if (IsProduction(invocation))
            {
                production.Add(invocation);
            }
        }

        var hasExpectedException = method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attribute => attribute.Name.ToString().Contains("ExpectedException", StringComparison.Ordinal));

        var flags = new List<string>();
        var evidence = new List<string>();

        if (assertions.Count == 0 && throws.Count == 0 && verifies.Count == 0 && !hasExpectedException)
        {
            var hasTryCatch = method.DescendantNodes().OfType<TryStatementSyntax>().Any();
            if (hasTryCatch)
            {
                AddFlag(flags, "T1");
                evidence.Add("try/catch swallows the exception and asserts nothing");
            }
            else if (production.Count > 0)
            {
                AddFlag(flags, "W3");
            }
            else
            {
                AddFlag(flags, "T1");
                evidence.Add("no assertion and no production call");
            }
        }
        else
        {
            var locals = CollectLocals(method);
            var mutated = CollectMutatedLocals(method);

            foreach (var assertion in assertions)
            {
                var name = GetMethodName(assertion) ?? string.Empty;
                var arguments = GetArguments(assertion);
                var normalized = arguments.Select(argument => Normalize(argument.ToString())).ToList();

                if (name is "AreEqual" or "AreSame" && arguments.Count == 2 && normalized[0] == normalized[1])
                {
                    AddFlag(flags, "T2");
                    evidence.Add($"identical assertion arguments: {Shorten(assertion.ToString())}");
                }

                if ((name == "IsTrue" && normalized.Count == 1 && normalized[0] == "true")
                    || (name == "IsFalse" && normalized.Count == 1 && normalized[0] == "false"))
                {
                    AddFlag(flags, "T3");
                    evidence.Add($"literal truth assertion: {Shorten(assertion.ToString())}");
                }

                if (WeakAssertions.Contains(name) && arguments.Count > 0 && arguments[0] is ObjectCreationExpressionSyntax)
                {
                    AddFlag(flags, "T4");
                    evidence.Add($"type check on freshly constructed value: {Shorten(assertion.ToString())}");
                }

                if (name is not ("AreEqual" or "AreSame") || arguments.Count < 2)
                {
                    continue;
                }

                var expected = normalized[0];
                var actual = arguments[1];

                if (actual is IdentifierNameSyntax identifier
                    && !mutated.Contains(identifier.Identifier.ValueText)
                    && locals.TryGetValue(identifier.Identifier.ValueText, out var initializer)
                    && IsLiteral(arguments[0]))
                {
                    if (initializer == expected)
                    {
                        AddFlag(flags, "T5");
                        evidence.Add($"asserted local echoes its own literal initializer: {identifier.Identifier.ValueText}");
                    }
                    else if (IsHelperInitializer(initializer))
                    {
                        AddFlag(flags, "T7");
                        evidence.Add($"assertion reads test-fixture value: {identifier.Identifier.ValueText}");
                    }
                }

                if (actual is InvocationExpressionSyntax call
                    && IsMockReceiver(GetReceiverText(call))
                    && IsLiteral(arguments[0])
                    && mockReturns.Contains(expected))
                {
                    AddFlag(flags, "T6");
                    evidence.Add($"assertion restates a mock's configured return: {Shorten(assertion.ToString())}");
                }
            }

            if (assertions.Count > 0
                && throws.Count == 0
                && verifies.Count == 0
                && assertions.All(a => WeakAssertions.Contains(GetMethodName(a) ?? string.Empty)))
            {
                AddFlag(flags, "W1");
            }

            if (assertions.Count == 0 && throws.Count == 0 && !hasExpectedException && verifies.Count > 0)
            {
                AddFlag(flags, "W2");
            }
        }

        var key = Normalize(
            string.Concat(method.AttributeLists.Select(list => list.ToString()))
            + "|"
            + (method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? string.Empty));

        var finding = new MethodFinding(
            RelativePath(file),
            method.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            className,
            method.Identifier.ValueText,
            flags,
            new AssertionCounts(assertions.Count, throws.Count, verifies.Count, production.Count),
            string.Join(" | ", evidence));

        return (finding, key);
    }

    private static Dictionary<string, string> CollectLocals(MethodDeclarationSyntax method)
    {
        var locals = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var declarator in method.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (declarator.Initializer is not null)
            {
                locals[declarator.Identifier.ValueText] = Normalize(declarator.Initializer.Value.ToString());
            }
        }

        foreach (var assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is IdentifierNameSyntax identifier)
            {
                locals[identifier.Identifier.ValueText] = Normalize(assignment.Right.ToString());
            }
        }

        return locals;
    }

    private static HashSet<string> CollectMutatedLocals(MethodDeclarationSyntax method)
    {
        var mutated = new HashSet<string>(StringComparer.Ordinal);

        foreach (var prefix in method.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>())
        {
            if (!prefix.IsKind(SyntaxKind.PreIncrementExpression)
                && !prefix.IsKind(SyntaxKind.PreDecrementExpression))
            {
                continue;
            }

            if (prefix.Operand is IdentifierNameSyntax identifier)
            {
                mutated.Add(identifier.Identifier.ValueText);
            }
        }

        foreach (var postfix in method.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>())
        {
            if (!postfix.IsKind(SyntaxKind.PostIncrementExpression)
                && !postfix.IsKind(SyntaxKind.PostDecrementExpression))
            {
                continue;
            }

            if (postfix.Operand is IdentifierNameSyntax identifier)
            {
                mutated.Add(identifier.Identifier.ValueText);
            }
        }

        foreach (var argument in method.DescendantNodes().OfType<ArgumentSyntax>())
        {
            if ((argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                    || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword))
                && argument.Expression is IdentifierNameSyntax identifier)
            {
                mutated.Add(identifier.Identifier.ValueText);
            }
        }

        foreach (var assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is IdentifierNameSyntax identifier)
            {
                mutated.Add(identifier.Identifier.ValueText);
            }
        }

        return mutated;
    }

    private static HashSet<string> CollectMockReturns(SyntaxNode root)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = GetMethodName(invocation);
            if (name is not ("Returns" or "ReturnsAsync"))
            {
                continue;
            }

            foreach (var argument in GetArguments(invocation))
            {
                if (IsLiteral(argument))
                {
                    values.Add(Normalize(argument.ToString()));
                }
            }
        }

        return values;
    }

    private static void AddFlag(List<string> flags, string flag)
    {
        if (!flags.Contains(flag, StringComparer.Ordinal))
        {
            flags.Add(flag);
        }
    }

    private static IReadOnlyList<ExpressionSyntax> GetArguments(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Select(argument => argument.Expression).ToList();

    private static string? GetMethodName(InvocationExpressionSyntax invocation)
        => invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => null,
        };

    private static string? GetReceiverText(InvocationExpressionSyntax invocation)
        => (invocation.Expression as MemberAccessExpressionSyntax)?.Expression.ToString();

    private static bool IsAssertion(InvocationExpressionSyntax invocation)
    {
        var name = GetMethodName(invocation) ?? string.Empty;
        var receiver = GetReceiverText(invocation)?.Split('.').LastOrDefault() ?? string.Empty;

        if (receiver is "Assert" or "CollectionAssert" or "StringAssert")
        {
            return true;
        }

        if (name.Length > "Assert".Length && name.StartsWith("Assert", StringComparison.Ordinal))
        {
            return true;
        }

        return name.StartsWith("Should", StringComparison.Ordinal);
    }

    private static bool IsThrowsAssertion(InvocationExpressionSyntax invocation)
    {
        var name = GetMethodName(invocation) ?? string.Empty;
        var receiver = GetReceiverText(invocation)?.Split('.').LastOrDefault() ?? string.Empty;
        return receiver.EndsWith("Assert", StringComparison.Ordinal)
            && name.StartsWith("Throws", StringComparison.Ordinal);
    }

    private static bool IsVerify(InvocationExpressionSyntax invocation)
    {
        var name = GetMethodName(invocation);
        return name is "Verify" or "VerifyNoOtherCalls" or "VerifyGet" or "VerifySet";
    }

    private static bool IsProduction(InvocationExpressionSyntax invocation)
    {
        var name = GetMethodName(invocation);
        if (name is null || Plumbing.Contains(name))
        {
            return false;
        }

        if (name.Length > "Assert".Length && name.StartsWith("Assert", StringComparison.Ordinal))
        {
            return false;
        }

        var receiver = GetReceiverText(invocation);
        if (IsMockReceiver(receiver))
        {
            return false;
        }

        if (receiver is not null
            && (receiver.Equals("Mock", StringComparison.Ordinal)
                || receiver.StartsWith("Mock.", StringComparison.Ordinal)
                || receiver.Equals("It", StringComparison.Ordinal)
                || receiver.StartsWith("It.", StringComparison.Ordinal)
                || receiver.Equals("Times", StringComparison.Ordinal)))
        {
            return false;
        }

        return true;
    }

    private static bool IsMockReceiver(string? receiver)
    {
        if (string.IsNullOrEmpty(receiver))
        {
            return false;
        }

        var lower = receiver.ToLowerInvariant();
        return lower.Contains("mock", StringComparison.Ordinal)
            || lower.EndsWith(".object", StringComparison.Ordinal)
            || lower.Contains("testhelpers", StringComparison.Ordinal)
            || lower.Contains("fixture", StringComparison.Ordinal)
            || lower.Contains("builder", StringComparison.Ordinal);
    }

    private static bool IsLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax;

    private static bool IsHelperInitializer(string initializer)
    {
        var match = Regex.Match(initializer, @"^(?:new\s+)?([A-Za-z_][\w.]*)\s*\(");
        if (!match.Success)
        {
            return false;
        }

        var typeOrCall = match.Groups[1].Value;
        return typeOrCall.Contains("TestHelpers", StringComparison.Ordinal)
            || typeOrCall.Contains("Fixture", StringComparison.Ordinal)
            || typeOrCall.Contains("Builder", StringComparison.Ordinal)
            || typeOrCall.Contains("Factory", StringComparison.Ordinal);
    }

    private static string Normalize(string value) => Whitespace.Replace(value, " ").Trim();

    private static string Shorten(string value)
    {
        var normalized = Normalize(value);
        return normalized.Length <= 100 ? normalized : normalized[..100] + "...";
    }
}
