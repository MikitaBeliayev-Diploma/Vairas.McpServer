using Vairas.McpServer.Business;
using Xunit;

namespace Vairas.McpServer.Tests;

/// <summary>Assertions on application errors by category; the HTTP status is derived exactly as the API layer maps it.</summary>
internal static class Errors
{
    public static async Task<BusinessException> ThrowsBusinessAsync(Func<Task> action) => await Assert.ThrowsAnyAsync<BusinessException>(action);
    public static BusinessException ThrowsBusiness(Action action) => Assert.ThrowsAny<BusinessException>(action);

    public static int HttpStatus(this BusinessException exception) => exception switch
    {
        BadRequestException => 400, UnauthorizedException => 401, ForbiddenException => 403, NotFoundException => 404,
        ConflictException => 409, UnprocessableEntityException => 422, ServiceUnavailableException => 503,
        _ => 500
    };
}
