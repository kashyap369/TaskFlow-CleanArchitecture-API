using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Enums.Identity;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Identity.Users
{
    public sealed class UserRepository : IUserRepository
    {
        private readonly TaskFlowDbContext _context;

        public UserRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<User?> GetByEmailAsync(
           Domain.ValueObjects.Email email,
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .FirstOrDefaultAsync(
                    x => x.Email.Value == email.Value,
                    cancellationToken);
        }

        public async Task<User?> GetByPhoneNumberAsync(
            PhoneNumber phoneNumber,
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .FirstOrDefaultAsync(
                    x => x.PhoneNumber.Value == phoneNumber.Value,
                    cancellationToken);
        }

        public async Task<bool> ExistsAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<bool> ExistsByEmailAsync(
           Domain.ValueObjects.Email email,
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AnyAsync(
                    x => x.Email.Value == email.Value,
                    cancellationToken);
        }

        public async Task<bool> ExistsByPhoneNumberAsync(
            PhoneNumber phoneNumber,
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AnyAsync(
                    x => x.PhoneNumber.Value == phoneNumber.Value,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<User>> GetByStatusAsync(
            UserStatus status,
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(x => x.Status == status)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<User>> GetUnverifiedUsersAsync(
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(x => !x.IsEmailVerified)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(
            User user,
            CancellationToken cancellationToken = default)
        {
            await _context.Users.AddAsync(
                user,
                cancellationToken);
        }

        public void Update(
            User user)
        {
            _context.Users.Update(user);
        }

        public void Remove(
            User user)
        {
            user.SoftDelete();

            _context.Users.Update(user);
        }
    }
}