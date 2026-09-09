using System.Reflection;

namespace JobTracker.Modules.Jobs.Presentation;

/// <summary>
/// A public handle on an assembly of internal endpoints. The composition root
/// needs an <see cref="Assembly"/> to scan and nothing else — naming
/// <c>typeof(CreateJob)</c> would mean widening an endpoint to public for the
/// convenience of one line in Program.cs.
/// </summary>
public static class JobsPresentation
{
    public static Assembly Assembly => typeof(JobsPresentation).Assembly;
}
