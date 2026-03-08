namespace Prism.Features.Models.Domain;

/// <summary>
/// Represents the operational status of a registered inference provider instance.
/// </summary>
public enum InstanceStatus
{
    /// <summary>Instance has been registered but not yet checked.</summary>
    Unknown,

    /// <summary>Instance is responding and ready to serve requests.</summary>
    Online,

    /// <summary>Instance is responding but with degraded performance or errors.</summary>
    Degraded,

    /// <summary>Instance is not responding to health checks.</summary>
    Offline
}
