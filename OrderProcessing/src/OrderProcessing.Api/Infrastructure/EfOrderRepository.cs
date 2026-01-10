using Microsoft.EntityFrameworkCore;
using OrderProcessing.Api.Data;
using OrderProcessing.Api.Models;

namespace OrderProcessing.Api.Infrastructure;

public sealed class EfOrderRepository : IOrderRepository, IOrderRepositorySync
{
    private readonly OrderDbContext _dbContext;

    public EfOrderRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async ValueTask<long> CreateAsync(OrderDraft order, CancellationToken ct)
    {
        // EF Core: produtividade alta com tracking. Para inserts simples, o tracking
        // é aceitável, mas pode ser desativado em cenários de alta escala.
        var entity = new OrderEntity
        {
            CustomerId = order.CustomerId,
            CreatedAtUtc = order.CreatedAtUtc,
            TotalAmount = order.Total.Amount,
            Currency = order.Total.Currency,
            Items = order.Lines
                .Select(line => new OrderItemEntity
                {
                    Sku = line.Sku,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice
                })
                .ToList()
        };

        _dbContext.Orders.Add(entity);

        // ConfigureAwait(false): útil em bibliotecas para evitar deadlocks em contextos
        // de sincronização; em ASP.NET Core o contexto é livre, mas mostramos o padrão.
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return entity.Id;
    }

    public long Create(OrderDraft order)
    {
        var entity = new OrderEntity
        {
            CustomerId = order.CustomerId,
            CreatedAtUtc = order.CreatedAtUtc,
            TotalAmount = order.Total.Amount,
            Currency = order.Total.Currency,
            Items = order.Lines
                .Select(line => new OrderItemEntity
                {
                    Sku = line.Sku,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice
                })
                .ToList()
        };

        _dbContext.Orders.Add(entity);
        _dbContext.SaveChanges();

        return entity.Id;
    }

    public async Task<OrderEntity?> GetByIdNoTrackingAsync(long id, CancellationToken ct)
    {
        // AsNoTracking: ideal para leituras que não precisam de mudança de estado.
        return await _dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(order => order.Id == id, ct)
            .ConfigureAwait(false);
    }
}
