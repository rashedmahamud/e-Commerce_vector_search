using ECommerceVectorSearch.Data;
using ECommerceVectorSearch.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Register the vector type with Npgsql so it can read/write Pgvector.Vector.
//var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("Default"));
//dataSourceBuilder.UseVector();
//var dataSource = dataSourceBuilder.Build();

//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseNpgsql(dataSource, o => o.UseVector()));

builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
