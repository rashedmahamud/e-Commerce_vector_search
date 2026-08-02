# E-Commerce Vector Similarity Search — ASP.NET Core 10 + pgvector

## What this is
- `Product` entity with a `vector(1024)` embedding column
- EF Core 10 + Npgsql, wired up for pgvector via `Pgvector.EntityFrameworkCore`
- An `IEmbeddingService` implemented against **Ollama**, serving the open-source
  `BAAI/bge-large-en-v1.5` model (`bge-large`) over its OpenAI-compatible `/v1/embeddings`
  endpoint — no OpenAI dependency, no API key, runs entirely on your own GPU/server
- Endpoints:
  - `POST /api/products` — create + auto-embed a product
  - `POST /api/products/search` — free-text semantic search ("cozy waterproof winter jacket")
  - `POST /api/products/similar` — "more like this" for an existing product
  - `POST /api/products/{id}/reindex` — re-embed after edits

## 1. Run Postgres with pgvector
```bash
docker run -d --name pg-vector \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=ecommerce \
  -p 5432:5432 \
  pgvector/pgvector:pg17
```

## 2. Enable the extension (once per database)
```bash
docker exec -it pg-vector psql -U postgres -d ecommerce -c "CREATE EXTENSION IF NOT EXISTS vector;"
```

## 3. Serve the embedding model with Ollama
On your Ollama host (here `103.165.162.229:11434`), pull the model once:
```bash
ollama pull bge-large
```
`bge-large` (BAAI/bge-large-en-v1.5, 335M params, 1024 dims) fits comfortably on an
8GB GPU (~1-2GB VRAM) and ranks near the top of the MTEB retrieval benchmarks —
it outperforms OpenAI's `text-embedding-ada-002` on most retrieval tasks.

`appsettings.json` already points `Embeddings:BaseUrl` at that server. No API key
is needed — Ollama doesn't require auth. If you run Ollama locally instead, change
`Embeddings:BaseUrl` to `http://localhost:11434/v1/`.

## 4. Create and apply the migration
```bash
dotnet tool install --global dotnet-ef   # if you don't have it
dotnet ef migrations add InitialCreate
dotnet ef database update
```
EF will generate the `products` table with a `vector(1024)` column and an HNSW index
(`vector_cosine_ops`) automatically, based on the model configuration in `AppDbContext`.

## 5. Run it
```bash
dotnet run
```
Swagger UI opens at `/swagger` in development.

## Notes on picking dimensions
The `vector(1024)` size in `AppDbContext.cs` must match your embedding model's output:
| Model (Ollama tag) | Dimensions | Notes |
|---|---|---|
| `bge-large` (BAAI/bge-large-en-v1.5) | 1024 | Default here — strong retrieval quality, ~1-2GB VRAM |
| `mxbai-embed-large` | 1024 | Also excellent, similar footprint |
| `nomic-embed-text` | 768 | Smaller/faster, still solid quality |
| `all-minilm` | 384 | Tiny, lowest quality of this list |

If you change models later, you'll need a migration to alter the column and re-embed all products.

## Notes on indexing at scale
- **HNSW** (used here) — better query performance and recall, higher build time/memory. Good default once you have more than a few thousand products.
- **IVFFlat** — faster to build, needs `lists` tuned to row count, and needs data present *before* building the index (`CREATE INDEX ... WITH (lists = N)` after a bulk load, not before).
- For catalogs under ~10k products, you can skip an ANN index entirely and do exact search — cosine distance on a few thousand rows is fast enough at query time.

## Notes on cosine distance in results
`CosineDistance` returns a value in `[0, 2]` (0 = identical direction, 2 = opposite).
The controller converts this to a similarity score (`1 - distance`) so `1.0` = perfect match,
which is usually more intuitive to show in a UI or use as a relevance threshold.

## Batch importing an existing catalog
For bulk backfill, call `IEmbeddingService.EmbedBatchAsync` in batches of ~100 products
instead of embedding one at a time — most embedding APIs accept batched input and it's
both cheaper and faster than a request-per-product loop.
