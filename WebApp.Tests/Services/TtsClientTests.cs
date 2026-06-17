using System.Net;
using System.Net.Http.Json;
using WebApp.Services;

namespace WebApp.Tests.Services;

public class TtsClientTests
{
    [Fact]
    public async Task SynthesizeAsync_PostsCorrectPayloadAndReturnsBytes()
    {
        var expectedBytes = new byte[] { 1, 2, 3 };
        string? capturedBody = null;

        var handler = new FakeHttpMessageHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expectedBytes)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav") }
                }
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://tts:5080") };
        var client = new TtsClient(httpClient);

        var result = await client.SynthesizeAsync("Hello", "pt", "female");

        Assert.Equal(expectedBytes, result);
        Assert.Contains("Hello", capturedBody);
        Assert.Contains("pt", capturedBody);
        Assert.Contains("female", capturedBody);
    }

    [Fact]
    public async Task SynthesizeAsync_WhenServiceReturnsError_Throws()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://tts:5080") };
        var client = new TtsClient(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SynthesizeAsync("Hello", "en", "male"));
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => handler(request);
    }
}
