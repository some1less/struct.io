using Mapster;
using Struct.BLL.DTOs;
using Struct.BLL.Services.Interfaces;
using Struct.DAL.Models;
using Struct.DAL.Repositories.Interfaces;

namespace Struct.BLL.Services.Implementations;

public class AccountService : IAccountService
{
    private readonly IAccountRepository _repository;

    public AccountService(IAccountRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<AccountDto>> GetPagedAsync(int page = 1, int pageSize = 50)
    {
        var entities = await _repository.GetPagedAsync(page, pageSize);
        return entities.Adapt<IEnumerable<AccountDto>>();
    }

    public async Task<AccountDto?> GetByIdAsync(int id)
    {
        var entity = await _repository.GetByIdAsync(id);
        return entity?.Adapt<AccountDto>();
    }

    public async Task<AccountDto> AddAsync(CreateAccountDto dto)
    {
        var entity = dto.Adapt<Account>();
        // Note: PasswordHash handling should be implemented here in the future
        entity.PasswordHash = "default_hash_for_now"; // Placeholder to pass DB constraints
        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return entity.Adapt<AccountDto>();
    }

    public async Task UpdateAsync(AccountDto dto)
    {
        var entity = await _repository.GetByIdAsync(dto.Id)
            ?? throw new KeyNotFoundException($"Account with ID {dto.Id} not found.");

        entity.Nickname = dto.Nickname;
        entity.Email = dto.Email;
        entity.ProfileId = dto.ProfileId;
        entity.RoleId = dto.RoleId;

        await _repository.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity != null)
        {
            _repository.Delete(entity);
            await _repository.SaveChangesAsync();
        }
    }
}
