using Pgvector;

namespace ECommerceVectorSearch.Models;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Sku { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Category { get; set; } = default!;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }

    // 1024 dims matches BAAI/bge-large-en-v1.5 served via Ollama.
    // Change to match whichever embedding model you use (e.g. 384, 768, 1536).
    public Vector? Embedding { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
