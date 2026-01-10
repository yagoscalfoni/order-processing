using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OrderProcessing.Api.Background;
using OrderProcessing.Api.Infrastructure;
using OrderProcessing.Api.Models;

namespace OrderProcessing.Api.Services;

public sealed class OrderProcessingHandler
{
    private readonly ILogger<OrderProcessingHandler> _logger;
    private readonly ITaxClient _taxClient;
    private readonly OrderMetricsChannel _metricsChannel;
    private readonly InventoryGate _inventoryGate;

    public OrderProcessingHandler(
        ILogger<OrderProcessingHandler> logger,
        ITaxClient taxClient,
        OrderMetricsChannel metricsChannel,
        InventoryGate inventoryGate)
    {
        _logger = logger;
        _taxClient = taxClient;
        _metricsChannel = metricsChannel;
        _inventoryGate = inventoryGate;
    }

    public async Task<OrderCreatedResponse> CreateOrderAsync(
        OrderRequest request,
        IOrderRepository repository,
        CancellationToken ct)
    {
        return await CreateOrderInternalAsync(
                request,
                (draft, token) => repository.CreateAsync(draft, token),
                ct)
            .ConfigureAwait(false);
    }

    public async Task<OrderCreatedResponse> CreateOrderAsyncWithSyncRepository(
        OrderRequest request,
        IOrderRepositorySync repository,
        CancellationToken ct)
    {
        return await CreateOrderInternalAsync(
                request,
                (draft, _) => ValueTask.FromResult(repository.Create(draft)),
                ct)
            .ConfigureAwait(false);
    }

    private async Task<OrderCreatedResponse> CreateOrderInternalAsync(
        OrderRequest request,
        Func<OrderDraft, CancellationToken, ValueTask<long>> createOrder,
        CancellationToken ct)
    {
        var validationErrors = new List<string>();
        await foreach (var error in ValidateAsync(request, ct).ConfigureAwait(false))
        {
            validationErrors.Add(error);
        }

        if (validationErrors.Count > 0)
        {
            _logger.LogWarning("Order validation failed: {Errors}", validationErrors);
            throw new InvalidOperationException(string.Join("; ", validationErrors));
        }

        // Coordenação assíncrona: SemaphoreSlim evita bloquear threads. Um lock seria
        // inadequado em async (poderia causar deadlocks e starvation).
        await _inventoryGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var createdAt = DateTimeOffset.UtcNow;
            var normalizedLines = NormalizeLines(request.Items);

            var total = CalculateTotal(normalizedLines, request.Currency);

            // ValueTask: custo menor se o resultado já estiver em cache ou pronto.
            var tax = await _taxClient.GetTaxAsync(total, ct).ConfigureAwait(false);
            var grandTotal = total + tax;

            var draft = new OrderDraft(request.CustomerId, createdAt, grandTotal, normalizedLines);

            var orderId = await createOrder(draft, ct).ConfigureAwait(false);

            _metricsChannel.TryWrite(new OrderMetric(orderId, grandTotal.Amount, createdAt));

            return new OrderCreatedResponse(orderId, createdAt, grandTotal.Amount, grandTotal.Currency);
        }
        finally
        {
            _inventoryGate.Release();
        }
    }

    private static IReadOnlyList<OrderLine> NormalizeLines(IReadOnlyList<OrderItemRequest> items)
    {
        // LINQ é preguiçoso (lazy). Aqui materializamos para evitar múltiplas enumerações.
        var materialized = items.ToArray();
        var normalized = new List<OrderLine>(materialized.Length);

        for (var i = 0; i < materialized.Length; i++)
        {
            // Evita armadilhas de closure: copie o valor para uma variável local
            // antes de usá-lo em lambdas/async.
            var item = materialized[i];

            var sku = SkuNormalizer.Normalize(item.Sku);
            normalized.Add(new OrderLine(sku, item.Quantity, item.UnitPrice));
        }

        return normalized;
    }

    private static Money CalculateTotal(IReadOnlyList<OrderLine> lines, string currency)
    {
        // readonly struct: reduz mutabilidade e garante semântica de valor.
        var accumulator = new OrderTotalAccumulator(currency);

        foreach (var line in lines)
        {
            accumulator = accumulator.Add(line.UnitPrice * line.Quantity);
        }

        return accumulator.Total;
    }

    private static async IAsyncEnumerable<string> ValidateAsync(
        OrderRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty)
        {
            yield return "CustomerId inválido";
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            yield return "Currency obrigatório";
        }

        if (request.Items.Count is 0)
        {
            yield return "É necessário pelo menos um item";
        }

        // Exemplo de pattern matching moderno (relational + logical patterns).
        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];

            if (item.Quantity is <= 0 or > 10_000)
            {
                yield return $"Quantidade inválida para SKU {item.Sku}";
            }

            if (item.UnitPrice is <= 0)
            {
                yield return $"Preço inválido para SKU {item.Sku}";
            }
        }

        // Simula validação assíncrona sem bloquear thread.
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
    }
}

public sealed class InventoryGate
{
    // SemaphoreSlim permite esperar sem bloquear threads (ao contrário de lock).
    private readonly SemaphoreSlim _semaphore = new(initialCount: 8, maxCount: 8);

    public Task WaitAsync(CancellationToken ct) => _semaphore.WaitAsync(ct);

    public void Release() => _semaphore.Release();
}

public interface ITaxClient
{
    ValueTask<Money> GetTaxAsync(Money subtotal, CancellationToken ct);
}

public sealed class SimulatedTaxClient : ITaxClient
{
    private static readonly ConcurrentDictionary<string, decimal> CachedRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BRL"] = 0.12m,
        ["USD"] = 0.07m
    };

    public SimulatedTaxClient(HttpClient _)
    {
    }

    public ValueTask<Money> GetTaxAsync(Money subtotal, CancellationToken ct)
    {
        // Exemplo de ValueTask: o cálculo é síncrono e trivial, sem necessidade de Task.
        var rate = CachedRates.GetValueOrDefault(subtotal.Currency, 0.05m);
        var tax = subtotal.Amount * rate;
        return ValueTask.FromResult(new Money(tax, subtotal.Currency));
    }
}

public readonly struct OrderTotalAccumulator
{
    public Money Total { get; }

    public OrderTotalAccumulator(string currency)
    {
        Total = new Money(0m, currency);
    }

    private OrderTotalAccumulator(Money total)
    {
        Total = total;
    }

    public OrderTotalAccumulator Add(decimal amount) => new(new Money(Total.Amount + amount, Total.Currency));
}

public static class SkuNormalizer
{
    public static string Normalize(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return string.Empty;
        }

        // Heap vs Stack:
        // - stackalloc: memória alocada na stack (mais barata, vida curta).
        // - new char[]: heap (gera trabalho para o GC).
        ReadOnlySpan<char> source = sku.AsSpan().Trim();
        Span<char> buffer = source.Length <= 64
            ? stackalloc char[source.Length]
            : new char[source.Length];

        for (var i = 0; i < source.Length; i++)
        {
            buffer[i] = char.ToUpperInvariant(source[i]);
        }

        // ref struct: tipo alocado na stack, ideal para spans temporários.
        var slice = new SkuSlice(buffer);
        return slice.ToStringValue();
    }

    public readonly ref struct SkuSlice
    {
        private readonly ReadOnlySpan<char> _span;

        public SkuSlice(ReadOnlySpan<char> span)
        {
            _span = span;
        }

        public string ToStringValue() => _span.ToString();
    }
}
