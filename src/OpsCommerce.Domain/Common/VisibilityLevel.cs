namespace OpsCommerce.Domain.Common;

/// <summary>Controls who can see a catalog item in a multi-tenant setup.</summary>
public enum VisibilityLevel
{
    Public = 0,
    CompanyOnly = 1,
    Private = 2
}
