﻿using System.Transactions;
using CorporateSystem.SharedDocs.Infrastructure.Options;
using Npgsql;

namespace CorporateSystem.SharedDocs.Infrastructure.Repositories;

internal abstract class PostgreRepository(PostgresOptions postgresOptions)
{
    protected const int DefaultTimeoutInSeconds = 5;
    protected abstract string TableName { get; }

    protected TransactionScope CreateTransactionScope(IsolationLevel level = IsolationLevel.ReadCommitted)
    {
        return new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions
            {
                IsolationLevel = level,
                Timeout = TimeSpan.FromSeconds(5)
            },
            TransactionScopeAsyncFlowOption.Enabled);
    }

    protected async Task<NpgsqlConnection> GetAndOpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(postgresOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ReloadTypesAsync(cancellationToken);
        return connection;
    }
}