using Struct.BLL.DTOs;

namespace Struct.BLL.Services.Interfaces;

public interface IAccountService
{
    Task<IEnumerable<AccountDto>> GetPagedAsync(int page = 1, int pageSize = 50);
    Task<AccountDto?> GetByIdAsync(int id);
    Task<AccountDto> AddAsync(CreateAccountDto dto);
    Task UpdateAsync(AccountDto dto);
    Task DeleteAsync(int id);
}
