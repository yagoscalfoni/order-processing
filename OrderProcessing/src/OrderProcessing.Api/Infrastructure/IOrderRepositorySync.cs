using OrderProcessing.Api.Models;

namespace OrderProcessing.Api.Infrastructure;

public interface IOrderRepositorySync
{
    long Create(OrderDraft order);
}
