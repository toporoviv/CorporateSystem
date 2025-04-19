using System.Transactions;
using CorporateSystem.Auth.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CorporateSystem.Auth.Infrastructure.Repositories.Implementations;

internal class ContextFactory(DbContextOptions<DataContext> options) : IContextFactory
{
    public async Task<T> ExecuteWithoutCommitAsync<T>(
        Func<DataContext, Task<T>> action,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var context = new DataContext(options);
        
        using var scope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = isolationLevel },
            TransactionScopeAsyncFlowOption.Enabled);

        var result = await action(context);
        scope.Complete();

        return result;
    }

    public async Task ExecuteWithCommitAsync(
        Func<DataContext, Task> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var context = new DataContext(options);
        
        using var scope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = isolationLevel },
            TransactionScopeAsyncFlowOption.Enabled);

        await action(context);
            
        await context.SaveChangesAsync(cancellationToken);
        scope.Complete();
    }

    public async Task<T> ExecuteWithCommitAsync<T>(
        Func<DataContext, Task<T>> action,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var context = new DataContext(options);
        
        using var scope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = isolationLevel },
            TransactionScopeAsyncFlowOption.Enabled);

        var result = await action(context);
            
        await context.SaveChangesAsync(cancellationToken);
        scope.Complete();
            
        return result;
    }
}