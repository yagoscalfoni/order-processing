# OrderProcessing

## Executando a aplicação com Docker Compose

1. Acesse a pasta do projeto:

   ```bash
   cd /workspace/order-processing/OrderProcessing
   ```

2. Suba os containers (SQL Server + API) e gere as imagens:

   ```bash
   docker compose up --build
   ```

3. A API ficará disponível em:

   ```
   http://localhost:8080
   ```

4. Para encerrar os containers:

   ```bash
   docker compose down
   ```

## Testando os endpoints da API

Você pode testar via Swagger UI ou usando `curl`.

### Swagger UI

Acesse no navegador:

```
http://localhost:8080/swagger
```

### Exemplos com curl

Os três endpoints aceitam o mesmo payload JSON:

```json
{
  "customerId": "11111111-1111-1111-1111-111111111111",
  "currency": "BRL",
  "items": [
    {
      "sku": "SKU-001",
      "quantity": 2,
      "unitPrice": 10.5
    }
  ]
}
```

#### EF Core

```bash
curl -X POST http://localhost:8080/orders/ef \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "11111111-1111-1111-1111-111111111111",
    "currency": "BRL",
    "items": [
      { "sku": "SKU-001", "quantity": 2, "unitPrice": 10.5 }
    ]
  }'
```

#### Dapper

```bash
curl -X POST http://localhost:8080/orders/dapper \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "11111111-1111-1111-1111-111111111111",
    "currency": "BRL",
    "items": [
      { "sku": "SKU-001", "quantity": 2, "unitPrice": 10.5 }
    ]
  }'
```

#### Stored Procedure

```bash
curl -X POST http://localhost:8080/orders/sp \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "11111111-1111-1111-1111-111111111111",
    "currency": "BRL",
    "items": [
      { "sku": "SKU-001", "quantity": 2, "unitPrice": 10.5 }
    ]
  }'
```
