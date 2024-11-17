using K7.Server.Application.Common.Models;
using Microsoft.AspNetCore.Identity;

namespace K7.Server.Infrastructure.Context.Identity;

public static class IdentityResultExtensions
{
    public static Result ToApplicationResult(this IdentityResult result)
    {
        return result.Succeeded
            ? Result.Success()
            : Result.Failure(result.Errors.Select(e => e.Description));
    }
}
