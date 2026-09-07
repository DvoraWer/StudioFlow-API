using AutoMapper;
using StudioFlow.Core.DTOs.Users;
using StudioFlow.Core.Exceptions;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Core.Interfaces.Services;

namespace StudioFlow.Service.Services;

/// <inheritdoc cref="IUserService" />
public sealed class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IMapper _mapper;

    public UserService(IUserRepository users, IMapper mapper)
    {
        _users = users;
        _mapper = mapper;
    }

    public async Task<UserResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken)
            ?? throw NotFoundException.For("User", id);

        return _mapper.Map<UserResponseDto>(user);
    }
}
