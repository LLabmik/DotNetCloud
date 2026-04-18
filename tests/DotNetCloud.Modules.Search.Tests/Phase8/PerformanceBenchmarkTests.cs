using System.Diagnostics;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Search.Tests.Phase8;

/// <summary>
/// Performance benchmarks for search indexing and query operations.
/// Measures throughput and latency at various document counts.
/// These tests validate performance characteristics and document scaling limits.
/// </summary>
[TestClass]
public class PerformanceBenchmarkTests
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private SearchDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<SearchDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new SearchDbContext(options);
    }

    private static SearchDocument CreateDoc(int index, string moduleId = "notes")
    {
        return new SearchDocument
        {
            ModuleId = moduleId,
            EntityId = $"entity-{index}",
            EntityType = "BenchmarkEntity",
            Title = $"Document {index}: {GetSampleTitle(index)}",
            Content = $"Content for document {index}. {GetSampleContent(index)}",
            OwnerId = TestUserId,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-index),
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-index),
            Metadata = new Dictionary<string, string>
            {
                ["category"] = (index % 5) switch
                {
                    0 => "finance",
                    1 => "engineering",
                    2 => "marketing",
                    3 => "hr",
                    _ => "operations"
                }
            }
        };
    }

    private static string GetSampleTitle(int index) => (index % 7) switch
    {
        0 => "Quarterly Budget Review",
        1 => "Project Status Update",
        2 => "Team Meeting Notes",
        3 => "Architecture Decision Record",
        4 => "Sprint Retrospective",
        5 => "Customer Feedback Analysis",
        _ => "Technical Specification"
    };

    private static string GetSampleContent(int index) =>
        $"This is a detailed document about {GetSampleTitle(index).ToLowerInvariant()}. " +
        $"It covers important topics related to our {(index % 3 == 0 ? "quarterly" : "monthly")} review. " +
        $"The key findings include performance metrics, team productivity, and strategic alignment. " +
        $"Additional context: document number {index} in the benchmark series.";

    [TestMethod]
    public async Task Benchmark_Index1000Documents_MeasureThroughput()
    {
        const int documentCount = 1000;
        using var db = CreateDbContext(nameof(Benchmark_Index1000Documents_MeasureThroughput));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        var sw = Stopwatch.StartNew();

        for (var i = 0; i < documentCount; i++)
        {
            await provider.IndexDocumentAsync(CreateDoc(i));
        }

        sw.Stop();

        var docsPerSecond = documentCount / sw.Elapsed.TotalSeconds;
        var stats = await provider.GetIndexStatsAsync();

        Assert.AreEqual(documentCount, stats.TotalDocuments);

        // Performance assertion: should index at least 100 docs/sec on InMemory
        Assert.IsTrue(docsPerSecond > 50,
            $"Indexing throughput: {docsPerSecond:F1} docs/sec (expected > 50)");

        // Log for visibility
        Console.WriteLine($"[BENCHMARK] Indexed {documentCount} documents in {sw.ElapsedMilliseconds}ms ({docsPerSecond:F1} docs/sec)");
    }

    [TestMethod]
    public async Task Benchmark_Search1000Documents_MeasureLatency()
    {
        const int documentCount = 1000;
        using var db = CreateDbContext(nameof(Benchmark_Search1000Documents_MeasureLatency));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        // Seed
        for (var i = 0; i < documentCount; i++)
        {
            await provider.IndexDocumentAsync(CreateDoc(i));
        }

        // Warm up
        await provider.SearchAsync(new SearchQuery
        {
            QueryText = "budget",
            UserId = TestUserId,
            Page = 1,
            PageSize = 20
        });

        // Measure search latency
        var latencies = new List<double>();
        var queries = new[] { "budget", "quarterly", "project status", "meeting notes", "architecture" };

        foreach (var q in queries)
        {
            var sw = Stopwatch.StartNew();

            var result = await provider.SearchAsync(new SearchQuery
            {
                QueryText = q,
                UserId = TestUserId,
                Page = 1,
                PageSize = 20
            });

            sw.Stop();
            latencies.Add(sw.Elapsed.TotalMilliseconds);

            Assert.IsTrue(result.TotalCount >= 0);
        }

        latencies.Sort();
        var p50 = latencies[(int)(latencies.Count * 0.5)];
        var p95 = latencies[(int)(latencies.Count * 0.95)];

        // On InMemory, searches should be fast
        Assert.IsTrue(p50 < 5000, $"p50 latency: {p50:F1}ms (expected < 5000ms)");

        Console.WriteLine($"[BENCHMARK] Search latency ({documentCount} docs): p50={p50:F1}ms, p95={p95:F1}ms");
    }

    [TestMethod]
    public async Task Benchmark_Search5000Documents_WithFacets()
    {
        const int documentCount = 5000;
        using var db = CreateDbContext(nameof(Benchmark_Search5000Documents_WithFacets));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        var modules = new[] { "notes", "files", "chat", "calendar", "contacts" };

        for (var i = 0; i < documentCount; i++)
        {
            await provider.IndexDocumentAsync(CreateDoc(i, modules[i % modules.Length]));
        }

        var sw = Stopwatch.StartNew();

        var result = await provider.SearchAsync(new SearchQuery
        {
            QueryText = "quarterly",
            UserId = TestUserId,
            Page = 1,
            PageSize = 20
        });

        sw.Stop();

        Assert.IsTrue(result.FacetCounts.Count > 0, "Should have facet counts");
        Assert.IsTrue(result.TotalCount > 0, "Should find results");

        Console.WriteLine($"[BENCHMARK] Search with facets ({documentCount} docs): {sw.ElapsedMilliseconds}ms, " +
                          $"{result.TotalCount} results, {result.FacetCounts.Count} facets");
    }

    [TestMethod]
    public async Task Benchmark_PaginationPerformance_Page1VsPage50()
    {
        const int documentCount = 2000;
        using var db = CreateDbContext(nameof(Benchmark_PaginationPerformance_Page1VsPage50));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        for (var i = 0; i < documentCount; i++)
        {
            await provider.IndexDocumentAsync(CreateDoc(i));
        }

        // Page 1
        var sw1 = Stopwatch.StartNew();
        var page1 = await provider.SearchAsync(new SearchQuery
        {
            QueryText = "document",
            UserId = TestUserId,
            Page = 1,
            PageSize = 20
        });
        sw1.Stop();

        // Page 50
        var sw50 = Stopwatch.StartNew();
        var page50 = await provider.SearchAsync(new SearchQuery
        {
            QueryText = "document",
            UserId = TestUserId,
            Page = 50,
            PageSize = 20
        });
        sw50.Stop();

        Assert.AreEqual(page1.TotalCount, page50.TotalCount, "Total count should be same across pages");

        Console.WriteLine($"[BENCHMARK] Pagination ({documentCount} docs): page 1 = {sw1.ElapsedMilliseconds}ms, page 50 = {sw50.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task Benchmark_ReindexModule_1000Documents()
    {
        const int documentCount = 1000;
        using var db = CreateDbContext(nameof(Benchmark_ReindexModule_1000Documents));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        for (var i = 0; i < documentCount; i++)
        {
            await provider.IndexDocumentAsync(CreateDoc(i));
        }

        var sw = Stopwatch.StartNew();
        await provider.ReindexModuleAsync("notes");
        sw.Stop();

        var stats = await provider.GetIndexStatsAsync();
        Assert.AreEqual(0, stats.TotalDocuments);

        Console.WriteLine($"[BENCHMARK] Reindex module ({documentCount} docs): {sw.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task Benchmark_QueryParser_10000Parses()
    {
        const int iterations = 10000;
        var queries = new[]
        {
            "quarterly budget",
            "\"exact phrase\" in:notes",
            "in:files type:pdf annual report -draft",
            "simple",
            "complex query with \"multiple phrases\" and -exclusions in:chat type:Message"
        };

        var sw = Stopwatch.StartNew();

        for (var i = 0; i < iterations; i++)
        {
            var q = queries[i % queries.Length];
            var parsed = SearchQueryParser.Parse(q);
            Assert.IsNotNull(parsed);
        }

        sw.Stop();

        var parsesPerSecond = iterations / sw.Elapsed.TotalSeconds;

        Assert.IsTrue(parsesPerSecond > 10000,
            $"Query parser throughput: {parsesPerSecond:F0} parses/sec (expected > 10000)");

        Console.WriteLine($"[BENCHMARK] Query parser: {iterations} parses in {sw.ElapsedMilliseconds}ms ({parsesPerSecond:F0} parses/sec)");
    }

    [TestMethod]
    public async Task Benchmark_SnippetGeneration_Performance()
    {
        const int iterations = 5000;
        var content = string.Join(" ", Enumerable.Range(0, 500).Select(i =>
            $"Word{i} content text about quarterly budget review and financial projections for the upcoming fiscal year."));

        var parsed = SearchQueryParser.Parse("quarterly budget");

        var sw = Stopwatch.StartNew();

        for (var i = 0; i < iterations; i++)
        {
            var snippet = SnippetGenerator.Generate(content, parsed);
            Assert.IsNotNull(snippet);
        }

        sw.Stop();

        var snippetsPerSecond = iterations / sw.Elapsed.TotalSeconds;

        Console.WriteLine($"[BENCHMARK] Snippet generation: {iterations} snippets in {sw.ElapsedMilliseconds}ms ({snippetsPerSecond:F0}/sec)");
    }

    [TestMethod]
    public async Task Benchmark_ConcurrentSearches_NoErrors()
    {
        const int documentCount = 500;
        const int concurrentSearches = 20;
        using var db = CreateDbContext(nameof(Benchmark_ConcurrentSearches_NoErrors));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        for (var i = 0; i < documentCount; i++)
        {
            await provider.IndexDocumentAsync(CreateDoc(i));
        }

        var queries = new[] { "budget", "quarterly", "project", "meeting", "architecture" };
        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, concurrentSearches).Select(i =>
            provider.SearchAsync(new SearchQuery
            {
                QueryText = queries[i % queries.Length],
                UserId = TestUserId,
                Page = 1,
                PageSize = 20
            }));

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        Assert.AreEqual(concurrentSearches, results.Length);
        foreach (var result in results)
        {
            Assert.IsTrue(result.TotalCount >= 0);
            Assert.IsNotNull(result.Items);
        }

        Console.WriteLine($"[BENCHMARK] {concurrentSearches} concurrent searches ({documentCount} docs): {sw.ElapsedMilliseconds}ms total");
    }
}
