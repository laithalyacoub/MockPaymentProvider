IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Payments] (
    [Id] int NOT NULL IDENTITY,
    [PaymentReference] AS CASE WHEN [Status] = 'SUCCESS' THEN 'PAY-' + CAST(50000 + [Id] AS nvarchar(20)) ELSE NULL END PERSISTED,
    [TransactionId] nvarchar(50) NOT NULL,
    [MobileNumber] nvarchar(10) NOT NULL,
    [Amount] decimal(10,2) NOT NULL,
    [Status] nvarchar(10) NOT NULL,
    [Message] nvarchar(200) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_Payments_TransactionId] ON [Payments] ([TransactionId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261107141203_InitialCreate', N'10.0.14');

COMMIT;
GO

