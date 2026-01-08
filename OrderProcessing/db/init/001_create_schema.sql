IF DB_ID('orders') IS NULL
BEGIN
    CREATE DATABASE orders;
END;
GO

USE orders;
GO

IF OBJECT_ID('dbo.orders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.orders (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        customer_id UNIQUEIDENTIFIER NOT NULL,
        created_at_utc DATETIMEOFFSET NOT NULL,
        total_amount DECIMAL(18,2) NOT NULL,
        currency NVARCHAR(10) NOT NULL
    );
END;
GO

IF OBJECT_ID('dbo.order_items', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.order_items (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        order_id BIGINT NOT NULL,
        sku NVARCHAR(64) NOT NULL,
        quantity INT NOT NULL,
        unit_price DECIMAL(18,2) NOT NULL,
        CONSTRAINT FK_order_items_orders FOREIGN KEY (order_id) REFERENCES dbo.orders(id)
    );
END;
GO

IF OBJECT_ID('dbo.create_order_sp', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.create_order_sp;
END;
GO

CREATE PROCEDURE dbo.create_order_sp
    @customer_id UNIQUEIDENTIFIER,
    @currency NVARCHAR(10),
    @total DECIMAL(18,2),
    @items NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.orders (customer_id, created_at_utc, total_amount, currency)
    VALUES (@customer_id, SYSUTCDATETIME(), @total, @currency);

    DECLARE @order_id BIGINT = CAST(SCOPE_IDENTITY() AS BIGINT);

    INSERT INTO dbo.order_items (order_id, sku, quantity, unit_price)
    SELECT
        @order_id,
        items.sku,
        items.quantity,
        items.unit_price
    FROM OPENJSON(@items)
    WITH (
        sku NVARCHAR(64) '$.sku',
        quantity INT '$.quantity',
        unit_price DECIMAL(18,2) '$.unit_price'
    ) AS items;

    SELECT @order_id;
END;
GO
