namespace HrDecisionSupport.Domain.Enums;

public enum EmploymentStatus
{
    Active = 1,
    OnLeave = 2,
    Terminated = 3
}

public enum CandidateSource
{
    ExternalSystem = 1,
    Ats = 2,
    Erp = 3,
    Excel = 4,
    Referral = 5,
    Other = 99
}

public enum CompetencyCategory
{
    Skill = 1,
    ProgrammingLanguage = 2,
    Framework = 3,
    Database = 4,
    Tool = 5,
    TechnicalConcept = 6
}

public enum CompetencyProficiencyLevel
{
    Beginner = 1,
    Elementary = 2,
    Intermediate = 3,
    Advanced = 4,
    Expert = 5
}

public enum LanguageProficiencyLevel
{
    A1 = 1,
    A2 = 2,
    B1 = 3,
    B2 = 4,
    C1 = 5,
    C2 = 6
}

public enum EmployeeDatasetSplit
{
    Training = 1,
    HoldoutTest = 2,
    Production = 3
}

public enum EmployeeImportBatchStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    CompletedWithErrors = 4,
    Failed = 5
}

public enum EmployeeImportRowStatus
{
    Pending = 1,
    Succeeded = 2,
    SucceededWithWarnings = 3,
    Failed = 4
}

public enum EmployeeCareerFeatureSource
{
    ImportedAggregate = 1,
    CalculatedFromEmploymentHistory = 2
}

public enum EmployeeRetentionLabelValue
{
    Short = 0,
    Normal = 1,
    Long = 2
}

public enum EmployeeRetentionLabelSource
{
    ImportedDataset = 1
}

public enum DegreeLevel
{
    HighSchool = 1,
    Associate = 2,
    Bachelor = 3,
    Master = 4,
    Doctorate = 5,
    Other = 99
}

public enum JobRequisitionStatus
{
    Draft = 1,
    Open = 2,
    OnHold = 3,
    Closed = 4,
    Cancelled = 5
}

public enum CandidateEvaluationStatus
{
    New = 1,
    InReview = 2,
    Interview = 3,
    Approved = 4,
    Rejected = 5,
    Withdrawn = 6,
    Closed = 7
}
