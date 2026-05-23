using Microsoft.EntityFrameworkCore;
using Struct.DAL.Context;
using Struct.DAL.Models;

namespace Struct.DAL.Repositories.Implementations;

public class BuildComponentRepository : IBuildComponentRepository
{
    private readonly AppDbContext _context;

    public BuildComponentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<BuildComponent>> GetPagedAsync(int page, int pageSize)
    {
        return await _context.BuildComponents
            .AsNoTracking()
            .OrderBy(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<BuildComponent?> GetByIdAsync(int id)
    {
        return await _context.BuildComponents.FindAsync(id);
    }

    public async Task AddAsync(BuildComponent entity)
    {
        await _context.BuildComponents.AddAsync(entity);
    }

    public void Update(BuildComponent entity)
    {
        _context.BuildComponents.Update(entity);
    }

    public void Delete(BuildComponent entity)
    {
        _context.BuildComponents.Remove(entity);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
