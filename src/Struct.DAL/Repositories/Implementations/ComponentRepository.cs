using Microsoft.EntityFrameworkCore;
using Struct.DAL.Context;
using Struct.DAL.Models;
using Struct.DAL.Repositories.Interfaces;

namespace Struct.DAL.Repositories.Implementations;

public class ComponentRepository : IComponentRepository
{
    private readonly AppDbContext _context;

    public ComponentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Component?> GetByIdAsync(int id)
    {
        return await _context.Components.FindAsync(id);
    }

    public async Task AddAsync(Component entity)
    {
        await _context.Components.AddAsync(entity);
    }

    public void Update(Component entity)
    {
        _context.Components.Update(entity);
    }

    public void Delete(Component entity)
    {
        _context.Components.Remove(entity);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<Component>> GetPagedAsync(int page, int pageSize)
    {
        return await _context.Components
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<Component>> GetByCategoryAsync(Category category, int page, int pageSize)
    {
        return await _context.Components
            .AsNoTracking()
            .Where(c => c.Category == category)
            .OrderBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}