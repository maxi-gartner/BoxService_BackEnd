using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Services;
using BoxService_BackEnd.Services.Abstractions;
using Npgsql;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

public static class ServiceInspectionEndpoints
{
    public static void MapServiceInspectionEndpoints(
        this WebApplication app
    )
    {
        var group = app.MapGroup("/services/{serviceId:int}/inspection")
            .RequireAuthorization(policy => policy.RequireRole("dueno", "empleado"))
            .AddEndpointFilter<TenantServiceFilter>();

        group.MapGet(
            "",
            async (
                int serviceId,
                ServiceInspectionService service,
                CancellationToken cancellationToken
            ) =>
            {
                var inspection =
                    await service.GetByServiceIdAsync(
                        serviceId,
                        cancellationToken
                    );

                return inspection is null
                    ? Error(
                        404,
                        "No se encontró la inspección."
                    )
                    : Ok(
                        ServiceInspectionDto.FromModel(
                            inspection
                        )
                    );
            }
        );

        group.MapPost(
            "",
            async (
                int serviceId,
                ServiceInspectionUpsertRequest request,
                HttpContext context,
                ServiceInspectionService service,
                CancellationToken cancellationToken
            ) =>
            {
                try
                {
                    var username =
                        context.User.Identity?.Name
                        ?? string.Empty;

                    var inspection =
                        await service.CreateAsync(
                            serviceId,
                            request,
                            username,
                            cancellationToken
                        );

                    return Ok(
                        ServiceInspectionDto.FromModel(
                            inspection
                        ),
                        201
                    );
                }
                catch (ArgumentException exception)
                {
                    return Error(
                        400,
                        exception.Message
                    );
                }
                catch (KeyNotFoundException exception)
                {
                    return Error(
                        404,
                        exception.Message
                    );
                }
                catch (PostgresException exception)
                    when (
                        exception.SqlState ==
                        PostgresErrorCodes.UniqueViolation
                    )
                {
                    return Error(
                        409,
                        "El service ya tiene una inspección."
                    );
                }
            }
        );

        group.MapPatch(
            "",
            async (
                int serviceId,
                ServiceInspectionUpsertRequest request,
                HttpContext context,
                ServiceInspectionService service,
                CancellationToken cancellationToken
            ) =>
            {
                try
                {
                    var username =
                        context.User.Identity?.Name
                        ?? string.Empty;

                    var inspection =
                        await service.UpdateAsync(
                            serviceId,
                            request,
                            username,
                            cancellationToken
                        );

                    return inspection is null
                        ? Error(
                            404,
                            "No se encontró la inspección."
                        )
                        : Ok(
                            ServiceInspectionDto.FromModel(
                                inspection
                            )
                        );
                }
                catch (ArgumentException exception)
                {
                    return Error(
                        400,
                        exception.Message
                    );
                }
            }
        );

        group.MapPost(
            "/photos",
            async (
                int serviceId,
                HttpRequest request,
                IInspectionPhotoService service,
                HttpContext context,
                CancellationToken cancellationToken
            ) =>
            {
                try
                {
                    if (!request.HasFormContentType)
                    {
                        return Error(
                            400,
                            "Se requiere un formulario multipart."
                        );
                    }

                    var form =
                        await request.ReadFormAsync(
                            cancellationToken
                        );

                    var file =
                        form.Files.GetFile("file");

                    if (file is null)
                    {
                        return Error(
                            400,
                            "La foto es obligatoria."
                        );
                    }

                    var photo =
                        await service.AddPhotoAsync(
                            serviceId,
                            file,
                            form["photoType"].ToString(),
                            form["description"].ToString(),
                            cancellationToken
                        );

                    return Ok(
                        new ServiceInspectionPhotoDto(
                            photo.PhotoId,
                            photo.StoragePath,
                            photo.PhotoType,
                            photo.Description,
                            photo.OriginalFileName,
                            photo.ContentType,
                            photo.CreatedAt
                        ),
                        201
                    );
                }
                catch (ArgumentException exception)
                {
                    return Error(
                        400,
                        exception.Message
                    );
                }
                catch (KeyNotFoundException exception)
                {
                    return Error(
                        404,
                        exception.Message
                    );
                }
                catch (InvalidOperationException exception)
                {
                    return StorageUnavailable(context, exception, "upload", serviceId);
                }
            }
        ).DisableAntiforgery();

        group.MapGet(
            "/photos/{photoId:int}/url",
            async (
                int serviceId,
                int photoId,
                IInspectionPhotoService service,
                HttpContext context,
                CancellationToken cancellationToken
            ) =>
            {
                try
                {
                    var url =
                        await service.CreatePhotoUrlAsync(
                            serviceId,
                            photoId,
                            cancellationToken
                        );

                    return url is null
                        ? Error(
                            404,
                            "No se encontró la foto."
                        )
                        : Ok(
                            new
                            {
                                url,
                                expiresIn = 900
                            }
                        );
                }
                catch (InvalidOperationException exception)
                {
                    return StorageUnavailable(context, exception, "signed-url", serviceId, photoId);
                }
            }
        );

        group.MapDelete(
            "/photos/{photoId:int}",
            async (
                int serviceId,
                int photoId,
                IInspectionPhotoService service,
                HttpContext context,
                CancellationToken cancellationToken
            ) =>
            {
                try
                {
                    var deleted =
                        await service.DeletePhotoAsync(
                            serviceId,
                            photoId,
                            cancellationToken
                        );

                    return deleted
                        ? Ok(
                            new
                            {
                                deleted = true
                            }
                        )
                        : Error(
                            404,
                            "No se encontró la foto."
                        );
                }
                catch (InvalidOperationException exception)
                {
                    return StorageUnavailable(context, exception, "delete", serviceId, photoId);
                }
            }
        );
    }

    private static IResult StorageUnavailable(HttpContext context, InvalidOperationException exception,
        string operation, int serviceId, int? photoId = null)
    {
        // Allowlist diagnostics: provider exception messages/inner exceptions can
        // contain credentials, signed URLs or configuration. Never log them raw.
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(ServiceInspectionEndpoints).FullName!);
        logger.LogError(new EventId(50301, "InspectionPhotoStorageUnavailable"),
            "Photo storage unavailable. Operation={Operation} ServiceId={ServiceId} PhotoId={PhotoId} ExceptionType={ExceptionType} TraceId={TraceId}",
            operation, serviceId, photoId, exception.GetType().FullName, context.TraceIdentifier);
        return Error(503, "El almacenamiento de fotografías no está disponible. Intentá nuevamente más tarde.");
    }
}
