using System.Collections.Generic;

namespace HrDecisionSupport.Application.CandidateImports.Models;

public sealed record CandidateImportResult(
    int Inserted,
    int SkippedExisting,
    int Failed,
    IReadOnlyList<CandidateImportError> Errors
);

public sealed record CandidateImportError(
    int RowNumber,
    string PropertyName,
    string Message
);
