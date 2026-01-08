using System.Threading.Channels;
namespace OrderProcessing.Api.Background;

public sealed record OrderMetric(long OrderId, decimal TotalAmount, DateTimeOffset CreatedAtUtc);

public sealed class OrderMetricsChannel
{
    private readonly Channel<OrderMetric> _channel = Channel.CreateUnbounded<OrderMetric>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryWrite(OrderMetric metric) => _channel.Writer.TryWrite(metric);

    public IAsyncEnumerable<OrderMetric> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public sealed class OrderMetricsReporter : BackgroundService
{
    private readonly ILogger<OrderMetricsReporter> _logger;
    private readonly OrderMetricsChannel _channel;

    public OrderMetricsReporter(ILogger<OrderMetricsReporter> logger, OrderMetricsChannel channel)
    {
        _logger = logger;
        _channel = channel;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Channel<T> + IAsyncEnumerable: coordenação assíncrona eficiente, sem locks.
        await foreach (var metric in _channel.ReadAllAsync(stoppingToken))
        {
            _logger.LogInformation(
                "Order processed {OrderId} total {TotalAmount} at {CreatedAtUtc}",
                metric.OrderId,
                metric.TotalAmount,
                metric.CreatedAtUtc);
        }
    }
}
