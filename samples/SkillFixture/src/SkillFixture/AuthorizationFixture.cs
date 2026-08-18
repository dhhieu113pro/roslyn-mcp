namespace SkillFixture.Controllers;

public abstract class ControllerBase;

[AttributeUsage(AttributeTargets.Method)]
public sealed class HttpGetAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public sealed class NonActionAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public sealed class BravoAuthorizeAttribute(string[] claims) : Attribute
{
    public string[] Claims { get; } = claims;
}

public static class BravoClaimConstants
{
    public const string Companies_Retrieve = "Companies_Retrieve";
    public const string Companies_All = "Companies_All";
}

public sealed class CompanyController : ControllerBase
{
    [HttpGet]
    public Task<string> SearchAsync() => Task.FromResult("ok");

    public Task<string> OverloadedAsync() => Task.FromResult("ok");
    public Task<string> OverloadedAsync(int id) => Task.FromResult(id.ToString());

    [NonAction]
    public void Helper() { }

    [BravoAuthorize(
        claims:
        [
            BravoClaimConstants.Companies_Retrieve,
            BravoClaimConstants.Companies_All
        ])]
    public void AlreadyAuthorized() { }

    [BravoAuthorize(
        claims:
        [
            BravoClaimConstants.Companies_Retrieve
        ])]
    public void ConflictingAuthorization() { }
}
