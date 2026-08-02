using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceVectorSearch.Migrations
{
    /// <inheritdoc />
    public partial class AddBm25Index : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_search CASCADE;");

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS idx_products_bm25
                ON products
                USING bm25 ("Id", "Name", "Description", "Category")
                WITH (key_field='Id');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_products_bm25;");
        }
    }
}
