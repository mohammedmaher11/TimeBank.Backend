using Microsoft.EntityFrameworkCore;
using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Domain.Common;

namespace TimeBank.Infrastructure.Persistence.Repositories;

public class Repository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    protected readonly TimeBankDbContext Context;
    protected readonly DbSet<TEntity> Set;

    public Repository(TimeBankDbContext context)
    {
        Context = context;
        Set = context.Set<TEntity>();
    }

    public virtual Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        Set.AddAsync(entity, cancellationToken).AsTask();

    public virtual void Remove(TEntity entity) => Set.Remove(entity);
}
