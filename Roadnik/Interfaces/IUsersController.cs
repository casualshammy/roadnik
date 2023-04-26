using Roadnik.Data;

namespace Roadnik.Interfaces;

public interface IUsersController
{
  Task AddUserAsync(string _key, string _email, CancellationToken _ct);
  Task DeleteUserAsync(string _key, CancellationToken _ct);
  Task<User?> GetUserAsync(string _key, CancellationToken _ct);
  Task<IReadOnlyList<User>> ListUsersAsync(CancellationToken _ct);
}
