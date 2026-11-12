using System.Net;
using System.Text;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Tracks.Tests.Web;

/// <summary>
/// The CSV wizard's browser side: local preview parsing, and the two host calls (dry run and import)
/// with their envelope handling.
/// </summary>
[TestClass]
public sealed class ModuleCsvImportTests
{
    private static readonly Guid ProductId = Guid.Parse("5f1d2c3b-0a49-4c2e-9a1e-1d2c3b4a5f60");
    private static readonly Guid SwimlaneId = Guid.Parse("9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d");

    private static ModuleCsvImport CreateImport(StubHttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://module.example/") },
            NullLogger<ModuleCsvImport>.Instance);

    private static Stream Csv(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    [TestMethod]
    public async Task ParseCsvAsync_QuotedFieldWithCommas_SplitsIntoOneField()
    {
        var import = CreateImport(StubHttpMessageHandler.Json("{}"));

        var result = await import.ParseCsvAsync(
            Csv("Title,Description\n\"Login, broken\",Cannot sign in\n"),
            CancellationToken.None);

        CollectionAssert.AreEqual(new List<string> { "Title", "Description" }, result.Headers);
        Assert.AreEqual(1, result.TotalRows);
        Assert.AreEqual("Login, broken", result.PreviewRows[0][0]);
        Assert.AreEqual("Cannot sign in", result.PreviewRows[0][1]);
    }

    [TestMethod]
    public async Task ParseCsvAsync_EscapedQuotes_Unescapes()
    {
        var import = CreateImport(StubHttpMessageHandler.Json("{}"));

        var result = await import.ParseCsvAsync(
            Csv("Title\n\"He said \"\"hello\"\"\"\n"),
            CancellationToken.None);

        Assert.AreEqual("He said \"hello\"", result.PreviewRows[0][0]);
    }

    [TestMethod]
    public async Task ParseCsvAsync_ParsesLocallyWithoutCallingHost()
    {
        var handler = StubHttpMessageHandler.Json("{}");

        await CreateImport(handler).ParseCsvAsync(Csv("Title\nA\n"), CancellationToken.None);

        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task ValidateCsvAsync_PostsDryRunAndMapsCounts()
    {
        var handler = StubHttpMessageHandler.Json(
            """{"success":true,"data":{"validRowCount":3,"errors":[{"rowNumber":4,"values":[],"errors":["Title is required"]}]}}""");

        var result = await CreateImport(handler).ValidateCsvAsync(
            ProductId,
            Csv("Title\nA\n"),
            new CsvColumnMapping { TitleColumn = 0 },
            CancellationToken.None);

        Assert.AreEqual(3, result.ValidRowCount);
        Assert.AreEqual(1, result.Errors.Count);
        Assert.AreEqual($"/api/v1/products/{ProductId}/work-items/import?dryRun=true", handler.Requests.Single().Uri);
    }

    [TestMethod]
    public async Task ImportCsvAsync_WithSwimlane_IncludesSwimlaneAndMapsRowErrors()
    {
        var handler = StubHttpMessageHandler.Json(
            """{"success":true,"data":{"created":5,"skipped":1,"failed":2,"batchCount":1,"errors":[{"rowNumber":7,"values":[],"errors":["Bad priority"]}]}}""");

        var result = await CreateImport(handler).ImportCsvAsync(
            ProductId,
            SwimlaneId,
            Csv("Title\nA\n"),
            new CsvColumnMapping { TitleColumn = 0 },
            skipDuplicates: true,
            CancellationToken.None);

        Assert.AreEqual(5, result.Created);
        Assert.AreEqual(1, result.Skipped);
        Assert.AreEqual(2, result.Failed);
        Assert.AreEqual(1, result.Errors.Count);
        Assert.AreEqual("Row 7: Bad priority", result.Errors[0]);
        Assert.AreEqual(
            $"/api/v1/products/{ProductId}/work-items/import?swimlaneId={SwimlaneId}",
            handler.Requests.Single().Uri);
    }

    [TestMethod]
    public async Task ImportCsvAsync_HostRejects_ThrowsHttpRequestException()
    {
        var handler = StubHttpMessageHandler.Json(
            """{"success":false,"error":{"code":"VALIDATION","message":"bad csv"}}""",
            HttpStatusCode.BadRequest);

        var ex = await Assert.ThrowsExactlyAsync<HttpRequestException>(() =>
            CreateImport(handler).ImportCsvAsync(
                ProductId,
                SwimlaneId,
                Csv("Title\nA\n"),
                new CsvColumnMapping { TitleColumn = 0 },
                skipDuplicates: false,
                CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    [TestMethod]
    public async Task ImportCsvAsync_EmptyEnvelope_ThrowsInvalidOperation()
    {
        var handler = StubHttpMessageHandler.Json("""{"success":true}""");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            CreateImport(handler).ImportCsvAsync(
                ProductId,
                SwimlaneId,
                Csv("Title\nA\n"),
                new CsvColumnMapping { TitleColumn = 0 },
                skipDuplicates: false,
                CancellationToken.None));
    }
}
