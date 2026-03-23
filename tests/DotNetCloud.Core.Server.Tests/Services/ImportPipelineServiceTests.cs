using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Import;
using DotNetCloud.Core.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

[TestClass]
public class ImportPipelineServiceTests
{
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestMethod]
    public void SupportedDataTypes_ReturnsRegisteredProviders()
    {
        var mockContacts = CreateMockProvider(ImportDataType.Contacts);
        var mockCalendar = CreateMockProvider(ImportDataType.CalendarEvents);
        var pipeline = CreatePipeline(mockContacts.Object, mockCalendar.Object);

        var types = pipeline.SupportedDataTypes;

        Assert.AreEqual(2, types.Count);
        Assert.IsTrue(types.Contains(ImportDataType.Contacts));
        Assert.IsTrue(types.Contains(ImportDataType.CalendarEvents));
    }

    [TestMethod]
    public async Task PreviewAsync_RoutesToCorrectProvider()
    {
        var expectedReport = CreateReport(ImportDataType.Contacts, isDryRun: true);
        var mockContacts = CreateMockProvider(ImportDataType.Contacts);
        mockContacts.Setup(p => p.PreviewAsync(It.IsAny<ImportRequest>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var pipeline = CreatePipeline(mockContacts.Object);
        var request = new ImportRequest { DataType = ImportDataType.Contacts, Data = "test" };

        var result = await pipeline.PreviewAsync(request, _caller);

        Assert.AreSame(expectedReport, result);
        mockContacts.Verify(p => p.PreviewAsync(request, _caller, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_RoutesToCorrectProvider()
    {
        var expectedReport = CreateReport(ImportDataType.CalendarEvents, isDryRun: false);
        var mockCalendar = CreateMockProvider(ImportDataType.CalendarEvents);
        mockCalendar.Setup(p => p.ExecuteAsync(It.IsAny<ImportRequest>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var pipeline = CreatePipeline(mockCalendar.Object);
        var request = new ImportRequest { DataType = ImportDataType.CalendarEvents, Data = "test" };

        var result = await pipeline.ExecuteAsync(request, _caller);

        Assert.AreSame(expectedReport, result);
        mockCalendar.Verify(p => p.ExecuteAsync(request, _caller, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_DryRun_DelegatesToPreview()
    {
        var expectedReport = CreateReport(ImportDataType.Contacts, isDryRun: true);
        var mockContacts = CreateMockProvider(ImportDataType.Contacts);
        mockContacts.Setup(p => p.PreviewAsync(It.IsAny<ImportRequest>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var pipeline = CreatePipeline(mockContacts.Object);
        var request = new ImportRequest { DataType = ImportDataType.Contacts, Data = "test", DryRun = true };

        var result = await pipeline.ExecuteAsync(request, _caller);

        Assert.AreSame(expectedReport, result);
        mockContacts.Verify(p => p.PreviewAsync(It.IsAny<ImportRequest>(), _caller, It.IsAny<CancellationToken>()), Times.Once);
        mockContacts.Verify(p => p.ExecuteAsync(It.IsAny<ImportRequest>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnsupportedDataType_ThrowsValidationException()
    {
        var mockContacts = CreateMockProvider(ImportDataType.Contacts);
        var pipeline = CreatePipeline(mockContacts.Object);
        var request = new ImportRequest { DataType = ImportDataType.Notes, Data = "test" };

        await Assert.ThrowsExactlyAsync<Errors.ValidationException>(
            () => pipeline.ExecuteAsync(request, _caller));
    }

    [TestMethod]
    public async Task PreviewAsync_UnsupportedDataType_ThrowsValidationException()
    {
        var pipeline = CreatePipeline(); // No providers
        var request = new ImportRequest { DataType = ImportDataType.Contacts, Data = "test" };

        await Assert.ThrowsExactlyAsync<Errors.ValidationException>(
            () => pipeline.PreviewAsync(request, _caller));
    }

    [TestMethod]
    public void SupportedDataTypes_NoProviders_ReturnsEmpty()
    {
        var pipeline = CreatePipeline();

        Assert.AreEqual(0, pipeline.SupportedDataTypes.Count);
    }

    [TestMethod]
    public void Constructor_DuplicateProviders_KeepsFirst()
    {
        var first = CreateMockProvider(ImportDataType.Contacts);
        var second = CreateMockProvider(ImportDataType.Contacts);
        var pipeline = CreatePipeline(first.Object, second.Object);

        Assert.AreEqual(1, pipeline.SupportedDataTypes.Count);
    }

    private static ImportPipelineService CreatePipeline(params IImportProvider[] providers)
    {
        return new ImportPipelineService(providers, NullLogger<ImportPipelineService>.Instance);
    }

    private static Mock<IImportProvider> CreateMockProvider(ImportDataType dataType)
    {
        var mock = new Mock<IImportProvider>();
        mock.Setup(p => p.DataType).Returns(dataType);
        return mock;
    }

    private static ImportReport CreateReport(ImportDataType dataType, bool isDryRun)
    {
        return new ImportReport
        {
            IsDryRun = isDryRun,
            DataType = dataType,
            Source = ImportSource.Generic,
            TotalItems = 0,
            SuccessCount = 0,
            SkippedCount = 0,
            FailedCount = 0,
            ConflictCount = 0,
            Items = [],
            ConflictStrategy = ImportConflictStrategy.Skip,
            StartedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow
        };
    }
}
