using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using TravelAgency.Domain.Models;
using TravelAgency.Repository.Data;
using TravelAgency.Repository.Interface;

namespace TravelAgency.Repository.Implementation;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly AppDbContext _context;
    private readonly DbSet<T> _entities;

    public Repository(AppDbContext context)
    {
        _context = context;
        _entities = _context.Set<T>();
    }

    public T Insert(T entity)
    {
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        _context.Add(entity);
        _context.SaveChanges();
        return entity;
    }

    public ICollection<T> InsertMany(ICollection<T> entity)
    {
        foreach (var e in entity) if (e.Id == Guid.Empty) e.Id = Guid.NewGuid();
        _context.AddRange(entity);
        _context.SaveChanges();
        return entity;
    }

    public T Update(T entity)
    {
        _context.Update(entity);
        _context.SaveChanges();
        return entity;
    }

    public T Delete(T entity)
    {
        _context.Remove(entity);
        _context.SaveChanges();
        return entity;
    }

    public E? Get<E>(Expression<Func<T, E>> selector,
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null)
    {
        IQueryable<T> q = _entities;
        if (include != null) q = include(q);
        if (predicate != null) q = q.Where(predicate);
        if (orderBy != null) return orderBy(q).Select(selector).FirstOrDefault();
        return q.Select(selector).FirstOrDefault();
    }

    public IEnumerable<E> GetAll<E>(Expression<Func<T, E>> selector,
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null)
    {
        IQueryable<T> q = _entities;
        if (include != null) q = include(q);
        if (predicate != null) q = q.Where(predicate);
        if (orderBy != null) return orderBy(q).Select(selector).AsEnumerable();
        return q.Select(selector).AsEnumerable();
    }
}
