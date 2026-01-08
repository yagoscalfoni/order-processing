namespace OrderProcessing.Api.Data;

public sealed class OrderEntity
{
    public long Id { get; set; }
    public Guid CustomerId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;

    public List<OrderItemEntity> Items { get; set; } = [];
}

public sealed class OrderItemEntity
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
