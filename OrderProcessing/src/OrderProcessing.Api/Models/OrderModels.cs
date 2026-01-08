namespace OrderProcessing.Api.Models;

public sealed record OrderRequest
{
    public required Guid CustomerId { get; init; }
    public required string Currency { get; init; }
    public required IReadOnlyList<OrderItemRequest> Items { get; init; }
}

public sealed record OrderItemRequest
{
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
}

public sealed record OrderCreatedResponse(
    long OrderId,
    DateTimeOffset CreatedAtUtc,
    decimal TotalAmount,
    string Currency);

// record struct: ótimo para tipos de valor pequenos e imutáveis.
public readonly record struct Money(decimal Amount, string Currency)
{
    public static Money operator +(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Currency mismatch.");
        }

        return new Money(left.Amount + right.Amount, left.Currency);
    }
}

public sealed record OrderDraft(
    Guid CustomerId,
    DateTimeOffset CreatedAtUtc,
    Money Total,
    IReadOnlyList<OrderLine> Lines);

public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice);
