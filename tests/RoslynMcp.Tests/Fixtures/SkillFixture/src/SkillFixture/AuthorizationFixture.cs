namespace SkillFixture.Controllers;

public abstract class ControllerBase;

[AttributeUsage(AttributeTargets.Method)]
public sealed class HttpGetAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public sealed class HttpPostAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public sealed class NonActionAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowAnonymousAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
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

    [HttpPost]
    public Task<string> CreateAsync(string name) => Task.FromResult(name);

    public Task<string> NoVerbAsync() => Task.FromResult("ok");

    public Task<string> OverloadedAsync() => Task.FromResult("ok");
    public Task<string> OverloadedAsync(int id) => Task.FromResult(id.ToString());

    [NonAction]
    public void Helper() { }

    [AllowAnonymous]
    [HttpGet]
    public void PublicStatus() { }

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

    [BravoAuthorize(
        [
            BravoClaimConstants.Companies_Retrieve
        ])]
    public void PositionalAuthorization() { }
}

[BravoAuthorize(
    claims:
    [
        BravoClaimConstants.Companies_All
    ])]
public sealed class SecuredController : ControllerBase
{
    [HttpGet]
    public void Dashboard() { }

    [HttpGet]
    [BravoAuthorize(
        claims:
        [
            BravoClaimConstants.Companies_Retrieve
        ])]
    public void Details() { }
}
