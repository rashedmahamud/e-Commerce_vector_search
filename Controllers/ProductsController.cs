using ECommerceVectorSearch.Data;
using ECommerceVectorSearch.DTOs;
using ECommerceVectorSearch.Models;
using ECommerceVectorSearch.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ECommerceVectorSearch.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
   
    private readonly IEmbeddingService _embeddings;

    public ProductsController( IEmbeddingService embeddings)
    {

        _embeddings = embeddings;
    }

    //// Create a product and embed it immediately (name + description gives the richest signal).
    //[HttpPost]
    //public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request, CancellationToken ct)
    //{
    //    var textToEmbed = $"{request.Name}. {request.Description}. Category: {request.Category}";
    //    var vector = await _embeddings.EmbedAsync(textToEmbed, ct);

    //    var product = new Product
    //    {
    //        Sku = request.Sku,
    //        Name = request.Name,
    //        Description = request.Description,
    //        Category = request.Category,
    //        Price = request.Price,
    //        ImageUrl = request.ImageUrl,
    //        Embedding = new Vector(vector)
    //    };

    //    _db.Products.Add(product);
    //    await _db.SaveChangesAsync(ct);

    //    return CreatedAtAction(nameof(GetById), new { id = product.Id }, ToResponse(product));
    //}
    [HttpPost]
    public async Task<ActionResult<List<GenerateEmbeddingsResponse>>> GenerateEmbeddings(
      [FromBody] List<GenerateEmbeddingsRequest> requests,
      CancellationToken ct)
    {
        if (requests is null || requests.Count == 0)
            return BadRequest("Request list is empty.");

        var vectorList = new List<GenerateEmbeddingsResponse>(requests.Count);

        foreach (var item in requests)
        {
            if (string.IsNullOrWhiteSpace(item.text))
                continue;

            var vector = await _embeddings.EmbedAsync(item.text, ct);

          vectorList.Add(new GenerateEmbeddingsResponse(item.id, vector.ToArray()));
        }

        return Ok(vectorList);
    }
    //[HttpGet("{id:guid}")]
    //public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken ct)
    //{
    //    var product = await _db.Products.FindAsync(new object[] { id }, ct);
    //    return product is null ? NotFound() : Ok(ToResponse(product));
    //}

    //// Free-text semantic search: "cozy waterproof hiking jacket for winter"
    //// -> embed the query, find nearest product vectors by cosine distance.
    //[HttpPost("search")]
    //public async Task<ActionResult<List<SimilarProductResponse>>> SearchByText(
    //    TextSearchRequest request, CancellationToken ct)
    //{
    //    var queryVector = new Vector(await _embeddings.EmbedAsync(request.Query, ct));

    //    var query = _db.Products.AsNoTracking();

    //    if (!string.IsNullOrWhiteSpace(request.Category))
    //        query = query.Where(p => p.Category == request.Category);

    //    var results = await query
    //        .Select(p => new
    //        {
    //            Product = p,
    //            Distance = p.Embedding!.CosineDistance(queryVector)
    //        })
    //        .OrderBy(x => x.Distance)
    //        .Take(Math.Clamp(request.TopK, 1, 100))
    //        .ToListAsync(ct);

    //    return Ok(results.Select(r => ToSimilarResponse(r.Product, r.Distance)).ToList());
    //}

    //// Hybrid search: combines pg_search BM25 (lexical precision — exact term/brand matches)
    //// with pgvector cosine similarity (semantic recall — typos, synonyms, "kind of like X").
    ////
    //// BM25 matches use conjunction_mode: true, requiring ALL query terms to appear together
    //// in the same field rather than matching on any single shared word. Without it, a generic
    //// word like "oil" alone was enough to rank groceries for a query like "engine oil", since
    //// BM25 weights short-field (Name) matches heavily and grocery names literally say "Oil"
    //// while motor product names don't. Requiring "engine"+"oil" together filters out matches
    //// on the shared word alone. If no field contains every term (typos, or terms split across
    //// fields), BM25 contributes nothing and vector search carries the result via GREATEST below.
    ////
    //// Fused via GREATEST rather than SUM: a product that's the #1 match on just one ranker
    //// still outranks two products that are merely decent on both — summed RRF let that pair
    //// "gang up" and bury the single best match. The summed score only breaks ties.
    //[HttpPost("search/hybrid")]
    //public async Task<ActionResult<List<HybridSearchResponse>>> HybridSearch(
    //    TextSearchRequest request, CancellationToken ct)
    //{
    //    var queryVector = new Vector(await _embeddings.EmbedAsync(request.Query, ct));
    //    var topK = Math.Clamp(request.TopK, 1, 100);
    //    var candidateSize = Math.Max(topK * 4, 20);
    //    var hasCategory = !string.IsNullOrWhiteSpace(request.Category);
    //    var bm25Query = BuildBm25Query(request.Query);

    //    var categoryFilter = hasCategory ? """ AND p."Category" = @category""" : "";

    //    var sql = $"""
    //        WITH fulltext AS (
    //            SELECT p."Id", 1.0 / (60 + ROW_NUMBER() OVER (ORDER BY paradedb.score(p."Id") DESC)) AS s
    //            FROM products p
    //            WHERE p."Id" @@@ paradedb.boolean(
    //                should => ARRAY[
    //                    paradedb.match('Name', @query, conjunction_mode => true),
    //                    paradedb.match('Description', @query, conjunction_mode => true),
    //                    paradedb.match('Category', @query, conjunction_mode => true)
    //                ]
    //            ){categoryFilter}
    //            LIMIT @candidateSize
    //        ),
    //        semantic AS (
    //            SELECT p."Id", 1.0 / (60 + ROW_NUMBER() OVER (ORDER BY p."Embedding" <=> @queryVector)) AS s
    //            FROM products p
    //            WHERE p."Embedding" IS NOT NULL{categoryFilter}
    //            LIMIT @candidateSize
    //        ),
    //        combined AS (
    //            SELECT
    //                COALESCE(f."Id", sem."Id") AS "Id",
    //                COALESCE(f.s, 0) AS bm25_score,
    //                COALESCE(sem.s, 0) AS vector_score
    //            FROM fulltext f
    //            FULL OUTER JOIN semantic sem ON f."Id" = sem."Id"
    //        )
    //        SELECT p."Id", p."Sku", p."Name", p."Category", p."Price", p."ImageUrl",
    //            GREATEST(bm25_score, vector_score) AS score
    //        FROM combined c
    //        JOIN products p ON p."Id" = c."Id"
    //        ORDER BY GREATEST(bm25_score, vector_score) DESC, (bm25_score + vector_score) DESC
    //        LIMIT @topK;
    //        """;

    //    // Connection is owned by the DbContext — open it via EF rather than disposing it ourselves.
    //    var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
    //    if (connection.State != System.Data.ConnectionState.Open)
    //        await _db.Database.OpenConnectionAsync(ct);

    //    await using var command = new NpgsqlCommand(sql, connection);
    //    command.Parameters.AddWithValue("query", bm25Query);
    //    command.Parameters.AddWithValue("queryVector", queryVector);
    //    command.Parameters.AddWithValue("candidateSize", candidateSize);
    //    command.Parameters.AddWithValue("topK", topK);
    //    if (hasCategory)
    //        command.Parameters.AddWithValue("category", request.Category!);

    //    var results = new List<HybridSearchResponse>();
    //    await using var reader = await command.ExecuteReaderAsync(ct);
    //    while (await reader.ReadAsync(ct))
    //    {
    //        results.Add(new HybridSearchResponse(
    //            reader.GetGuid(0),
    //            reader.GetString(1),
    //            reader.GetString(2),
    //            reader.GetString(3),
    //            reader.GetDecimal(4),
    //            reader.IsDBNull(5) ? null : reader.GetString(5),
    //            reader.GetDouble(6)));
    //    }

    //    return Ok(results);
    //}

    //// "More like this" / "customers also liked" — nearest neighbors of an existing product's vector.
    //[HttpPost("similar")]
    //public async Task<ActionResult<List<SimilarProductResponse>>> SimilarToProduct(
    //    SimilarToProductRequest request, CancellationToken ct)
    //{
    //    var source = await _db.Products.AsNoTracking()
    //        .FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);

    //    if (source?.Embedding is null)
    //        return NotFound($"Product {request.ProductId} not found or has no embedding yet.");

    //    var query = _db.Products.AsNoTracking();
    //    if (request.ExcludeSelf)
    //        query = query.Where(p => p.Id != request.ProductId);

    //    var results = await query
    //        .Select(p => new
    //        {
    //            Product = p,
    //            Distance = p.Embedding!.CosineDistance(source.Embedding)
    //        })
    //        .OrderBy(x => x.Distance)
    //        .Take(Math.Clamp(request.TopK, 1, 100))
    //        .ToListAsync(ct);

    //    return Ok(results.Select(r => ToSimilarResponse(r.Product, r.Distance)).ToList());
    //}

    //// Re-embed a product after editing its name/description.
    //[HttpPost("{id:guid}/reindex")]
    //public async Task<IActionResult> Reindex(Guid id, CancellationToken ct)
    //{
    //    var product = await _db.Products.FindAsync(new object[] { id }, ct);
    //    if (product is null) return NotFound();

    //    var text = $"{product.Name}. {product.Description}. Category: {product.Category}";
    //    var vector = await _embeddings.EmbedAsync(text, ct);
    //    product.Embedding = new Vector(vector);
    //    product.UpdatedAt = DateTime.UtcNow;

    //    await _db.SaveChangesAsync(ct);
    //    return NoContent();
    //}

    private static readonly Regex BareNumberToken = new(@"\b\d+\b", RegexOptions.Compiled);

    // Bare numbers (e.g. "1") are near-meaningless for BM25 here — they show up both as
    // quantities ("1 liter") and inside brand names ("Mobil 1"), so matching on them alone
    // lets unrelated products from other categories leak into lexical results. The vector
    // side still gets the full query text, since embeddings use "1 liter" as real context.
    private static string BuildBm25Query(string query)
    {
        var stripped = BareNumberToken.Replace(query, " ").Trim();
        return string.IsNullOrWhiteSpace(stripped) ? query : stripped;
    }

    private static ProductResponse ToResponse(Product p) =>
        new(p.Id, p.Sku, p.Name, p.Description, p.Category, p.Price, p.ImageUrl);

    // pgvector CosineDistance returns 0 (identical) .. 2 (opposite). Similarity = 1 - distance.
    private static SimilarProductResponse ToSimilarResponse(Product p, double distance) =>
        new(p.Id, p.Sku, p.Name, p.Category, p.Price, p.ImageUrl, 1.0 - distance);
}
