using System.Net;
using System.Text;
using System.Text.Json;
using DotNetCloud.Core.AI;
using DotNetCloud.Modules.AI.Data.Services;
using DotNetCloud.Modules.AI.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace DotNetCloud.Modules.AI.Tests;

/// <summary>
/// Tests for <see cref="OllamaClient"/>.
/// Uses a mocked HttpMessageHandler to test without a real Ollama instance.
/// </summary>
[TestClass]
public class OllamaClientTests
{
    private Mock<HttpMessageHandler> _handlerMock;
    private HttpClient _httpClient;
    private OllamaClient _client;

    [TestInitialize]
    public void Setup()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var settingsProvider = new Mock<IAiSettingsProvider>();
        settingsProvider.Setup(s => s.GetApiBaseUrlAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://localhost:11434/");

        _client = new OllamaClient(_httpClient, settingsProvider.Object, NullLogger<OllamaClient>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _httpClient.Dispose();
    }

    [TestMethod]
    public async Task IsHealthy_SuccessResponse_ReturnsTrue()
    {
        SetupResponse(HttpStatusCode.OK, "Ollama is running", "/");

        var result = await _client.IsHealthyAsync();

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task IsHealthy_FailedResponse_ReturnsFalse()
    {
        SetupResponse(HttpStatusCode.ServiceUnavailable, "Error", "/");

        var result = await _client.IsHealthyAsync();

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task IsHealthy_ConnectionError_ReturnsFalse()
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await _client.IsHealthyAsync();

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ChatAsync_ValidRequest_ReturnsResponse()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            model = "gpt-oss:20b",
            message = new { role = "assistant", content = "Hello there!" },
            done = true,
            total_duration = 1234567890L,
            prompt_eval_count = 10,
            eval_count = 5
        });

        SetupResponse(HttpStatusCode.OK, responseJson, "/api/chat");

        var request = new LlmRequest
        {
            Model = "gpt-oss:20b",
            Messages = new[] { new LlmMessage("user", "Hello") }
        };

        var response = await _client.ChatAsync(request);

        Assert.AreEqual("gpt-oss:20b", response.Model);
        Assert.AreEqual("Hello there!", response.Message.Content);
        Assert.AreEqual("assistant", response.Message.Role);
        Assert.IsTrue(response.Done);
    }

    [TestMethod]
    public async Task ListModels_ReturnsModelList()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            models = new[]
            {
                new
                {
                    name = "gpt-oss:20b",
                    size = 12345678L,
                    modified_at = "2026-01-01T00:00:00Z",
                    details = new { parameter_size = "20B", quantization_level = "Q4_K_M" }
                },
                new
                {
                    name = "llama3:8b",
                    size = 9876543L,
                    modified_at = "2026-02-01T00:00:00Z",
                    details = new { parameter_size = "8B", quantization_level = "Q4_K_M" }
                }
            }
        });

        SetupResponse(HttpStatusCode.OK, responseJson, "/api/tags");

        var models = await _client.ListModelsAsync();

        Assert.AreEqual(2, models.Count);
        Assert.AreEqual("gpt-oss:20b", models[0].Id);
        Assert.AreEqual("ollama", models[0].Provider);
        Assert.AreEqual("llama3:8b", models[1].Id);
    }

    [TestMethod]
    public async Task ChatAsync_WithSystemPrompt_IncludesInRequest()
    {
        HttpRequestMessage? capturedRequest = null;

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        model = "gpt-oss:20b",
                        message = new { role = "assistant", content = "I'm a coding assistant." },
                        done = true
                    }),
                    Encoding.UTF8, "application/json")
            });

        var request = new LlmRequest
        {
            Model = "gpt-oss:20b",
            Messages = new[] { new LlmMessage("user", "What are you?") },
            SystemPrompt = "You are a coding assistant."
        };

        await _client.ChatAsync(request);

        Assert.IsNotNull(capturedRequest);
        var body = await capturedRequest.Content!.ReadAsStringAsync();
        Assert.IsTrue(body.Contains("coding assistant"));
    }

    private void SetupResponse(HttpStatusCode statusCode, string content, string expectedPath)
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.PathAndQuery.StartsWith(expectedPath)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }
}
