using Microsoft.EntityFrameworkCore;
using Struct.DAL.Context;
using Struct.DAL.Models;

namespace Struct.DAL.Repositories;

public class ComponentRepository : IComponentRepository
{
    private readonly AppDbContext _context;

    public ComponentRepository(AppDbContext context)
    {
        _context = context;
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
}