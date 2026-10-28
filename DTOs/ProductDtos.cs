namespace ECommerceVectorSearch.DTOs;

public record CreateProductRequest(
    string Sku,
    string Name,
    string Description,
    string Category,
    decimal Price,
    string? ImageUrl
);

public record GenerateEmbeddingsRequest(
    int id,
    string text

  
);
public record GenerateEmbeddingsResponse(
      int id,
     float[] textEmbedding
);

public record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    string Category,
    decimal Price,
    string? ImageUrl
);

public record SimilarProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string Category,
    decimal Price,
    string? ImageUrl,
    double Similarity // 1.0 = identical, 0.0 = unrelated (cosine similarity)
);

public record TextSearchRequest(string Query, int TopK = 10, string? Category = null);

public record SimilarToProductRequest(Guid ProductId, int TopK = 10, bool ExcludeSelf = true);

public record HybridSearchResponse(
    Guid Id,
    string Sku,
    string Name,
    string Category,
    decimal Price,
    string? ImageUrl,
    double Score // Reciprocal Rank Fusion score (BM25 rank + vector rank) — higher is more relevant, not a 0-1 similarity
);
