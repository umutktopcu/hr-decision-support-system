using HrDecisionSupport.Application.Candidates.Dtos;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.Candidates;

public sealed class CandidateService : ICandidateService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly IValidator<CreateCandidateRequest> _createValidator;
    private readonly IValidator<UpdateCandidateRequest> _updateValidator;

    public CandidateService(
        IHrDecisionSupportDbContext dbContext,
        IValidator<CreateCandidateRequest> createValidator,
        IValidator<UpdateCandidateRequest> updateValidator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
    }

    public async Task<Result<IReadOnlyList<CandidateListItemDto>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var candidates = await _dbContext.Candidates
            .AsNoTracking()
            .OrderBy(candidate => candidate.CandidateCode)
            .ThenBy(candidate => candidate.Id)
            .Select(candidate => new CandidateListItemDto(
                candidate.Id,
                candidate.CandidateCode,
                candidate.Person.AnonymousCode,
                candidate.Person.FirstName,
                candidate.Person.LastName,
                candidate.Person.Email,
                candidate.CandidateSource,
                candidate.ExternalCandidateId))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<CandidateListItemDto>>.Success(candidates);
    }

    public async Task<Result<CandidateDetailsDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var candidate = await _dbContext.Candidates
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new CandidateDetailsDto(
                item.Id,
                item.PersonId,
                item.CandidateCode,
                item.Person.AnonymousCode,
                item.Person.FirstName,
                item.Person.LastName,
                item.Person.Email,
                item.Person.PhoneNumber,
                item.CandidateSource,
                item.ExternalCandidateId,
                item.EvaluationCases.Count))
            .SingleOrDefaultAsync(cancellationToken);

        return candidate is null
            ? Result<CandidateDetailsDto>.Failure(UseCaseErrors.CandidateNotFound)
            : Result<CandidateDetailsDto>.Success(candidate);
    }

    public async Task<Result<CandidateDetailsDto>> CreateAsync(
        CreateCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = _createValidator.Validate(request);
        if (!validation.IsValid)
        {
            return Result<CandidateDetailsDto>.ValidationFailure(validation.Errors);
        }

        var candidateCode = request.CandidateCode.Trim();
        var anonymousCode = request.AnonymousCode.Trim();
        var externalCandidateId = request.ExternalCandidateId?.Trim();
        var personData = PersonData.Create(
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber);

        if (await _dbContext.Candidates.AnyAsync(
                candidate => candidate.CandidateCode == candidateCode,
                cancellationToken))
        {
            return Result<CandidateDetailsDto>.Failure(UseCaseErrors.CandidateCodeConflict);
        }

        var person = await _dbContext.People.SingleOrDefaultAsync(
            item => item.AnonymousCode == anonymousCode,
            cancellationToken);
        var utcNow = DateTime.UtcNow;

        if (person is null)
        {
            person = personData.CreatePerson(anonymousCode, utcNow);
            await _dbContext.People.AddAsync(person, cancellationToken);
        }
        else
        {
            if (await _dbContext.Candidates.AnyAsync(
                    candidate => candidate.PersonId == person.Id,
                    cancellationToken))
            {
                return Result<CandidateDetailsDto>.Failure(UseCaseErrors.PersonAlreadyCandidate);
            }

            if (personData.ConflictsWith(person))
            {
                return Result<CandidateDetailsDto>.Failure(UseCaseErrors.PersonDataConflict);
            }

            if (personData.CompleteMissingFields(person))
            {
                person.UpdatedAtUtc = utcNow;
            }
        }

        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            Person = person,
            CandidateCode = candidateCode,
            CandidateSource = request.CandidateSource,
            ExternalCandidateId = externalCandidateId
        };

        await _dbContext.Candidates.AddAsync(candidate, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<CandidateDetailsDto>.Success(MapDetails(candidate, person, 0));
    }

    public async Task<Result<CandidateDetailsDto>> UpdateAsync(
        Guid id,
        UpdateCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var candidate = await _dbContext.Candidates
            .Include(item => item.Person)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (candidate is null)
        {
            return Result<CandidateDetailsDto>.Failure(UseCaseErrors.CandidateNotFound);
        }

        var validation = _updateValidator.Validate(request);
        if (!validation.IsValid)
        {
            return Result<CandidateDetailsDto>.ValidationFailure(validation.Errors);
        }

        var candidateCode = request.CandidateCode.Trim();
        if (candidate.CandidateCode != candidateCode
            && await _dbContext.Candidates.AnyAsync(
                item => item.Id != id && item.CandidateCode == candidateCode,
                cancellationToken))
        {
            return Result<CandidateDetailsDto>.Failure(UseCaseErrors.CandidateCodeConflict);
        }

        candidate.CandidateCode = candidateCode;
        candidate.CandidateSource = request.CandidateSource;
        candidate.ExternalCandidateId = request.ExternalCandidateId?.Trim();

        PersonData.Create(request.FirstName, request.LastName, request.Email, request.PhoneNumber)
            .ApplyTo(candidate.Person);
        candidate.Person.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private static CandidateDetailsDto MapDetails(
        Candidate candidate,
        Person person,
        int evaluationCaseCount) =>
        new(
            candidate.Id,
            person.Id,
            candidate.CandidateCode,
            person.AnonymousCode,
            person.FirstName,
            person.LastName,
            person.Email,
            person.PhoneNumber,
            candidate.CandidateSource,
            candidate.ExternalCandidateId,
            evaluationCaseCount);
}
