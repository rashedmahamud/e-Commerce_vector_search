using ECommerceVectorSearch.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerceVectorSearch.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");

            entity.HasKey(p => p.Id);
            entity.Property(p => p.Sku).HasMaxLength(64).IsRequired();
            entity.HasIndex(p => p.Sku).IsUnique();
            entity.Property(p => p.Name).HasMaxLength(300).IsRequired();
            entity.Property(p => p.Category).HasMaxLength(120);
            entity.Property(p => p.Price).HasColumnType("numeric(10,2)");

            // vector(1024) column — dimension MUST match your embedding model's output.
            entity.Property(p => p.Embedding).HasColumnType("vector(1024)");

            // HNSW index for fast approximate nearest-neighbor search using cosine distance.
            // Requires pgvector >= 0.5.0. Alternative: "vector_l2_ops" or "vector_ip_ops".
            entity.HasIndex(p => p.Embedding)
                  .HasMethod("hnsw")
                  .HasOperators("vector_cosine_ops")
                  .HasStorageParameter("m", 16)
                  .HasStorageParameter("ef_construction", 64);
        });
    }
}
