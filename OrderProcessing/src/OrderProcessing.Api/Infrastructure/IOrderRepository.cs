using OrderProcessing.Api.Models;

namespace OrderProcessing.Api.Infrastructure;

public interface IOrderRepository
{
    // ValueTask: evita alocação quando o resultado já está disponível.
    ValueTask<long> CreateAsync(OrderDraft order, CancellationToken ct);
}
