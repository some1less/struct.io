using Struct.DAL.Models;
using Struct.DAL.Repositories.Interfaces;

namespace Struct.Tests.TestSupport;

/// <summary>
/// In-memory <see cref="IComponentRepository"/> backed by a fixed catalog. Lets the
/// recommendation engine run end-to-end without Postgres. Only the read paths the engine
/// actually uses are meaningfully implemented; mutating members are no-ops.
/// </summary>
public sealed class StubComponentRepository : IComponentRepository
{
    private readonly List<Component> _catalog;

    public StubComponentRepository(IEnumerable<Component> catalog)
    {
        _catalog = catalog.ToList();
        for (int i = 0; i < _catalog.Count; i++)
            _catalog[i].Id = i + 1; // stable ids so DTO mapping has something to map
    }

    public Task<IEnumerable<Component>> GetByCategoryAsync(Category category, int page, int pageSize)
    {
        var rows = _catalog.Where(c => c.Category == category)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsEnumerable();
        return Task.FromResult(rows);
    }

    public Task<IEnumerable<Component>> GetPagedAsync(int page, int pageSize) =>
        Task.FromResult(_catalog.Skip((page - 1) * pageSize).Take(pageSize).AsEnumerable());

    public Task<Component?> GetByIdAsync(int id) =>
        Task.FromResult(_catalog.FirstOrDefault(c => c.Id == id));

    public Task AddAsync(Component entity) { _catalog.Add(entity); return Task.CompletedTask; }
    public void Update(Component entity) { }
    public void Delete(Component entity) { _catalog.Remove(entity); }
    public Task SaveChangesAsync() => Task.CompletedTask;
}
