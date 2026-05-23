using Microsoft.EntityFrameworkCore;
using Struct.DAL.Context;
using Struct.DAL.Models;

namespace Struct.DAL.Repositories.Implementations;

public class SavedBuildRepository : ISavedBuildRepository
{
    private readonly AppDbContext _context;

    public SavedBuildRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SavedBuild>> GetPagedAsync(int page, int pageSize)
    {
        return await _context.SavedBuilds
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<SavedBuild?> GetByIdAsync(int id)
    {
        return await _context.SavedBuilds.FindAsync(id);
    }

    public async Task AddAsync(SavedBuild entity)
    {
        await _context.SavedBuilds.AddAsync(entity);
    }

    public void Update(SavedBuild entity)
    {
        _context.SavedBuilds.Update(entity);
    }

    public void Delete(SavedBuild entity)
    {
        _context.SavedBuilds.Remove(entity);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
