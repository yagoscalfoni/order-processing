using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using OrderProcessing.Api.Models;

namespace OrderProcessing.Api.Infrastructure;

public sealed class StoredProcedureOrderRepository : IOrderRepository
{
    private readonly string _connectionString;

    public StoredProcedureOrderRepository(string connectionString)
    {
        _connectionString = connectionString
            ?? throw new InvalidOperationException("Missing Orders connection string.");
    }

    public async ValueTask<long> CreateAsync(OrderDraft order, CancellationToken ct)
    {
        // Stored Procedure: lógica empacotada no banco. Útil para controle de versionamento
        // ou regras sensíveis de dados, mas reduz flexibilidade e aumenta acoplamento.
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = new SqlCommand("create_order_sp", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@customer_id", order.CustomerId);
        command.Parameters.AddWithValue("@currency", order.Total.Currency);
        command.Parameters.AddWithValue("@total", order.Total.Amount);

        var itemsJson = JsonSerializer.Serialize(order.Lines.Select(line => new
        {
            sku = line.Sku,
            quantity = line.Quantity,
            unit_price = line.UnitPrice
        }));

        command.Parameters.Add("@items", SqlDbType.NVarChar, -1).Value = itemsJson;

        var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt64(result);
    }
}
