using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.CandidateImports.Models;

namespace HrDecisionSupport.Application.CandidateImports.Spreadsheet;

public interface ICandidateImportSourceReader
{
    Task<IReadOnlyList<CandidateInputRecord>> ReadRecordsAsync(
        Stream sourceStream,
        CancellationToken cancellationToken = default);
}
