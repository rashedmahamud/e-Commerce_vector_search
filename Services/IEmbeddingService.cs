namespace ECommerceVectorSearch.Services;

public interface IEmbeddingService
{
    /// <summary>Generates a single embedding vector for the given text.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    /// <summary>Generates embeddings for a batch of texts in one call (cheaper/faster than looping).</summary>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
