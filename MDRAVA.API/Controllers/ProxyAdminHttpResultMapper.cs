using System.Text;
using MDRAVA.BLL.ControlPlane;
using MDRAVA.BLL.ControlPlane.Metrics;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

internal static class ProxyAdminHttpResultMapper
{
    public static ActionResult<T> OkOrBadRequest<T>(
        ControllerBase controller,
        T result,
        bool succeeded)
    {
        return succeeded ? controller.Ok(result) : controller.BadRequest(result);
    }

    public static ActionResult<T> OkOrNotFound<T>(
        ControllerBase controller,
        T? result)
        where T : class
    {
        return result is null ? controller.NotFound() : controller.Ok(result);
    }

    public static ActionResult<T> OkOrNotFound<T>(
        ControllerBase controller,
        bool found,
        T? result)
        where T : class
    {
        return found && result is not null ? controller.Ok(result) : controller.NotFound();
    }

    public static IActionResult TextExportOrNotFound(
        ControllerBase controller,
        ProxyMetricsExportResult result)
    {
        return result.Available
            ? controller.Content(result.Content, result.ContentType, Encoding.UTF8)
            : controller.NotFound();
    }
}
