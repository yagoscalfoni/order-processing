using Microsoft.EntityFrameworkCore;
using OrderProcessing.Api.Background;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.Infrastructure;
using OrderProcessing.Api.Models;
using OrderProcessing.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Minimal APIs: configurações enxutas e foco em produtividade/clareza.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<OrderDbContext>(options =>
{
    // SqlServer + EF Core para SQL Server.
    options.UseSqlServer(builder.Configuration.GetConnectionString("Orders"));

    // Dica didática: log do SQL gerado (útil para comparar EF vs Dapper vs SP).
    options.EnableSensitiveDataLogging();
});

// HttpClientFactory: evita exaustão de sockets e centraliza políticas.
builder.Services.AddHttpClient<ITaxClient, SimulatedTaxClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(2);
});

builder.Services.AddSingleton<OrderMetricsChannel>();
builder.Services.AddSingleton<InventoryGate>();

builder.Services.AddScoped<OrderProcessingHandler>();
builder.Services.AddScoped<EfOrderRepository>();
builder.Services.AddScoped<DapperOrderRepository>();
builder.Services.AddScoped<StoredProcedureOrderRepository>();

builder.Services.AddHostedService<OrderMetricsReporter>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/orders/ef", async (
    OrderRequest request,
    OrderProcessingHandler handler,
    EfOrderRepository repository,
    CancellationToken ct) =>
{
    return Results.Ok(await handler.CreateOrderAsync(request, repository, ct));
});

app.MapPost("/orders/dapper", async (
    OrderRequest request,
    OrderProcessingHandler handler,
    DapperOrderRepository repository,
    CancellationToken ct) =>
{
    return Results.Ok(await handler.CreateOrderAsync(request, repository, ct));
});

app.MapPost("/orders/sp", async (
    OrderRequest request,
    OrderProcessingHandler handler,
    StoredProcedureOrderRepository repository,
    CancellationToken ct) =>
{
    return Results.Ok(await handler.CreateOrderAsync(request, repository, ct));
});

app.Run();

// Observação importante sobre async void:
// Em aplicações server-side, *nunca* use async void; isso torna exceções
// inobserváveis e dificulta o controle do fluxo. Sempre retorne Task/ValueTask.
