using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ECommerceVectorSearch.Services;

// Talks to Ollama's OpenAI-compatible /v1/embeddings endpoint.
// Default model: bge-large (BAAI/bge-large-en-v1.5, 1024 dims) — open-source,
// strong on retrieval benchmarks, and small enough to run on an 8GB GPU.
// Pull it on the Ollama host first: `ollama pull bge-large`
public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly string _model;

    public OllamaEmbeddingService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _model = config["Embeddings:Model"] ?? "bge-large";
        _http.BaseAddress ??= new Uri(config["Embeddings:BaseUrl"] ?? "http://localhost:11434/v1/");
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var results = await EmbedBatchAsync(new[] { text }, ct);
        return results[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var request = new EmbeddingRequest(_model, texts.ToArray());

        var response = await _http.PostAsJsonAsync("embeddings", request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from Ollama embeddings API.");

        return payload.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToList();
    }

    private record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string[] Input);

    private record EmbeddingResponse(
        [property: JsonPropertyName("data")] List<EmbeddingData> Data);

    private record EmbeddingData(
        [property: JsonPropertyName("embedding")] float[] Embedding,
        [property: JsonPropertyName("index")] int Index);
}
