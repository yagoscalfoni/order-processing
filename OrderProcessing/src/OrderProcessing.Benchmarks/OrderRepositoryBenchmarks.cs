using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderProcessing.Api.Background;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.Infrastructure;
using OrderProcessing.Api.Models;
using OrderProcessing.Api.Services;

namespace OrderProcessing.Benchmarks;

[MemoryDiagnoser]
public class OrderRepositoryBenchmarks
{
    // BenchmarkDotNet roda em processo isolado
    // A connection string PRECISA ser explícita
    private const string OrdersConnectionString =
        "Server=sqlserver,1433;" +
        "Database=orders;" +
        "User Id=sa;" +
        "Password=Your_password123;" +
        "TrustServerCertificate=True";

    private readonly OrderMetricsChannel _metricsChannel = new();
    private readonly InventoryGate _inventoryGate = new();
    private readonly ITaxClient _taxClient = new SimulatedTaxClient(
        new HttpClient { Timeout = TimeSpan.FromSeconds(2) });

    private OrderProcessingHandler? _handler;
    private DbContextOptions<OrderDbContext>? _dbOptions;
    private OrderRequest? _request;

    [Params(1, 5, 20)]
    public int ItemsCount { get; set; }

    // Executado uma vez por combinação de parâmetros
    [GlobalSetup]
    public void GlobalSetup()
    {
        _dbOptions = CreateDbOptions();

        _handler = new OrderProcessingHandler(
            NullLogger<OrderProcessingHandler>.Instance,
            _taxClient,
            _metricsChannel,
            _inventoryGate);

        _request = BuildRequest(ItemsCount);
    }

    [Benchmark]
    public async Task CreateOrderEf()
    {
        var handler = _handler!;
        var request = _request!;
        var options = _dbOptions!;

        await using var dbContext = new OrderDbContext(options);
        var repository = new EfOrderRepository(dbContext);

        await handler.CreateOrderAsync(request, repository, CancellationToken.None);
    }

    [Benchmark]
    public async Task CreateOrderDapper()
    {
        var handler = _handler!;
        var request = _request!;

        var repository = new DapperOrderRepository(OrdersConnectionString);
        await handler.CreateOrderAsync(request, repository, CancellationToken.None);
    }

    [Benchmark]
    public async Task CreateOrderStoredProcedure()
    {
        var handler = _handler!;
        var request = _request!;

        var repository = new StoredProcedureOrderRepository(OrdersConnectionString);
        await handler.CreateOrderAsync(request, repository, CancellationToken.None);
    }

    private static DbContextOptions<OrderDbContext> CreateDbOptions()
    {
        return new DbContextOptionsBuilder<OrderDbContext>()
            .UseSqlServer(OrdersConnectionString)
            .Options;
    }

    private static OrderRequest BuildRequest(int itemsCount)
    {
        var items = new List<OrderItemRequest>(itemsCount);

        for (var i = 0; i < itemsCount; i++)
        {
            items.Add(new OrderItemRequest
            {
                Sku = $"SKU-{i + 1:000}",
                Quantity = 1 + (i % 3),
                UnitPrice = 10m + i
            });
        }

        return new OrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Currency = "USD",
            Items = items
        };
    }
}
