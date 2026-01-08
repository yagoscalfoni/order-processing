using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly OrderMetricsChannel _metricsChannel = new();
    private readonly InventoryGate _inventoryGate = new();
    private readonly ITaxClient _taxClient = new SimulatedTaxClient(new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(2)
    });

    private OrderProcessingHandler? _handler;
    private IConfigurationRoot? _configuration;
    private DbContextOptions<OrderDbContext>? _dbOptions;
    private OrderRequest? _request;

    [Params(1, 5, 20)]
    public int ItemsCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _configuration = BuildConfiguration();
        _dbOptions = CreateDbOptions(_configuration);
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
        var handler = _handler ?? throw new InvalidOperationException("Handler not initialized.");
        var request = _request ?? throw new InvalidOperationException("Request not initialized.");
        var options = _dbOptions ?? throw new InvalidOperationException("DB options not initialized.");

        await using var dbContext = new OrderDbContext(options);
        var repository = new EfOrderRepository(dbContext);

        await handler.CreateOrderAsync(request, repository, CancellationToken.None);
    }

    [Benchmark]
    public async Task CreateOrderDapper()
    {
        var handler = _handler ?? throw new InvalidOperationException("Handler not initialized.");
        var request = _request ?? throw new InvalidOperationException("Request not initialized.");
        var configuration = _configuration ?? throw new InvalidOperationException("Configuration not initialized.");

        var repository = new DapperOrderRepository(configuration);
        await handler.CreateOrderAsync(request, repository, CancellationToken.None);
    }

    [Benchmark]
    public async Task CreateOrderStoredProcedure()
    {
        var handler = _handler ?? throw new InvalidOperationException("Handler not initialized.");
        var request = _request ?? throw new InvalidOperationException("Request not initialized.");
        var configuration = _configuration ?? throw new InvalidOperationException("Configuration not initialized.");

        var repository = new StoredProcedureOrderRepository(configuration);
        await handler.CreateOrderAsync(request, repository, CancellationToken.None);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var basePath = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(basePath, "appsettings.json"),
            Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "OrderProcessing.Api", "appsettings.json"))
        };

        var builder = new ConfigurationBuilder();
        var loaded = false;

        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            builder.AddJsonFile(candidate, optional: false, reloadOnChange: false);
            loaded = true;
            break;
        }

        if (!loaded)
        {
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        }

        builder.AddEnvironmentVariables();
        return builder.Build();
    }

    private static DbContextOptions<OrderDbContext> CreateDbOptions(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Orders");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Missing Orders connection string.");
        }

        return new DbContextOptionsBuilder<OrderDbContext>()
            .UseSqlServer(connectionString)
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
