using TimeBank.Application.Abstractions.Events;
using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.Abstractions.Security;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Auth;
using TimeBank.Application.Factories;
using TimeBank.Application.Mapping;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberSkillService _memberSkillService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IDateTimeProvider _clock;

    public AuthService(
        IUnitOfWork unitOfWork,
        IMemberSkillService memberSkillService,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IDateTimeProvider clock)
    {
        _unitOfWork = unitOfWork;
        _memberSkillService = memberSkillService;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _clock = clock;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Skills is null || request.Skills.Count == 0)
            throw new BusinessRuleException("Choose at least one skill when joining.");

        if (request.Skills.GroupBy(s => s.SkillId).Any(g => g.Count() > 1))
            throw new BusinessRuleException("A skill can only be selected once.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var passwordHash = _passwordHasher.Hash(request.Password);
        var now = _clock.UtcNow;
        AuthResponse? response = null;

        await _unitOfWork.ExecuteInTransactionAsync(null, async ct =>
        {
            if (await _unitOfWork.Members.EmailExistsAsync(normalizedEmail, ct))
                throw new ConflictException("This email address is already registered.");

            var member = Member.Create(
                request.FullName,
                normalizedEmail,
                passwordHash,
                request.District,
                request.Bio,
                request.PhotoPath,
                MemberRole.Member,
                now);

            await _unitOfWork.Members.AddAsync(member, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _memberSkillService.SetSkillsAsync(member.Id, request.Skills, ct);

            // BR-02: every new member receives 2 welcome hours as a ledger entry.
            var welcomeBonus = LedgerEntryFactory.WelcomeBonus(member.Id, now);
            await _unitOfWork.LedgerEntries.AddAsync(welcomeBonus, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var token = _jwtTokenService.CreateToken(member);
            response = new AuthResponse(token.Token, token.ExpiresAt, member.ToSummary());
        }, cancellationToken);

        return response ?? throw new InvalidOperationException("Registration did not complete.");
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var member = await _unitOfWork.Members.GetByEmailAsync(email, cancellationToken);

        // Do not reveal whether the email exists.
        if (member is null || !_passwordHasher.Verify(request.Password, member.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        // BR-14: suspended members cannot log in.
        member.EnsureActive();

        var token = _jwtTokenService.CreateToken(member);
        return new AuthResponse(token.Token, token.ExpiresAt, member.ToSummary());
    }
}
