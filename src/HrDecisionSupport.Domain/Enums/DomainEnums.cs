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

public enum ProficiencyLevel
{
    Beginner = 1,
    Elementary = 2,
    Intermediate = 3,
    Advanced = 4,
    Expert = 5
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
