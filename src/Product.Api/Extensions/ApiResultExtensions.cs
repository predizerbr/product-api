using Microsoft.AspNetCore.Mvc;
using Product.Business.Interfaces.Results;
using Product.Contracts;

namespace Product.Api.Extensions;

public static class ApiResultExtensions
{
    public static IActionResult ToActionResult(this ControllerBase controller, ApiResult result)
    {
        if (result.StatusCode == StatusCodes.Status204NoContent)
        {
            return new StatusCodeResult(StatusCodes.Status204NoContent);
        }

        if (result.StatusCode >= StatusCodes.Status400BadRequest)
        {
            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                return controller.Problem(statusCode: result.StatusCode, title: result.Error);
            }

            return controller.StatusCode(result.StatusCode, result.Data);
        }

        if (!result.Envelope && result.Data is null)
        {
            return new StatusCodeResult(result.StatusCode);
        }

        var body = result.Envelope
            ? new ResponseEnvelope<object?>(result.Data, result.Meta)
            : result.Data;

        return controller.StatusCode(result.StatusCode, body);
    }
}
