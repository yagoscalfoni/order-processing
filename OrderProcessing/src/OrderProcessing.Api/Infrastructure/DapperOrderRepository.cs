using Dapper;
using Microsoft.Data.SqlClient;
using OrderProcessing.Api.Models;

namespace OrderProcessing.Api.Infrastructure;

public sealed class DapperOrderRepository : IOrderRepository, IOrderRepositorySync
{
    private readonly string _connectionString;

    public DapperOrderRepository(string connectionString)
    {
        _connectionString = connectionString
            ?? throw new InvalidOperationException("Missing Orders connection string.");
    }

    public async ValueTask<long> CreateAsync(OrderDraft order, CancellationToken ct)
    {
        const string insertOrderSql = """
            INSERT INTO orders (customer_id, created_at_utc, total_amount, currency)
            VALUES (@CustomerId, @CreatedAtUtc, @TotalAmount, @Currency);
            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """;

        const string insertItemsSql = """
            INSERT INTO order_items (order_id, sku, quantity, unit_price)
            VALUES (@OrderId, @Sku, @Quantity, @UnitPrice);
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        // Dapper: SQL explícito, zero tracking, overhead menor.
        var orderId = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                insertOrderSql,
                new
                {
                    order.CustomerId,
                    order.CreatedAtUtc,
                    TotalAmount = order.Total.Amount,
                    Currency = order.Total.Currency
                },
                transaction,
                cancellationToken: ct)).ConfigureAwait(false);

        foreach (var line in order.Lines)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    insertItemsSql,
                    new
                    {
                        OrderId = orderId,
                        line.Sku,
                        line.Quantity,
                        line.UnitPrice
                    },
                    transaction,
                    cancellationToken: ct)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return orderId;
    }

    public long Create(OrderDraft order)
    {
        const string insertOrderSql = """
            INSERT INTO orders (customer_id, created_at_utc, total_amount, currency)
            VALUES (@CustomerId, @CreatedAtUtc, @TotalAmount, @Currency);
            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """;

        const string insertItemsSql = """
            INSERT INTO order_items (order_id, sku, quantity, unit_price)
            VALUES (@OrderId, @Sku, @Quantity, @UnitPrice);
            """;

        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var orderId = connection.ExecuteScalar<long>(
            insertOrderSql,
            new
            {
                order.CustomerId,
                order.CreatedAtUtc,
                TotalAmount = order.Total.Amount,
                Currency = order.Total.Currency
            },
            transaction);

        foreach (var line in order.Lines)
        {
            connection.Execute(
                insertItemsSql,
                new
                {
                    OrderId = orderId,
                    line.Sku,
                    line.Quantity,
                    line.UnitPrice
                },
                transaction);
        }

        transaction.Commit();
        return orderId;
    }
}
