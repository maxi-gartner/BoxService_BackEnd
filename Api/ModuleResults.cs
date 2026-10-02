using Npgsql;

namespace BoxService_BackEnd.Api;

internal static class ModuleResults
{
    public static IResult Ok(object data, int status = 200) =>
        Results.Json(ApiEnvelope<object>.Ok(data), statusCode: status);

    public static IResult Error(int status, string message) =>
        Results.Json(ApiEnvelope<object>.Fail(status, message), statusCode: status);

    // Unexpected failures reach the global 500 handler; only input errors become 400.
    public static IResult Write(Func<IResult> action)
    {
        try { return action(); }
        catch (ArgumentException ex) { return Error(400, ex.Message); }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        { return Error(400, "Referenced resource does not exist or is still in use."); }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        { return Error(400, "Resource already exists."); }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.StringDataRightTruncation or PostgresErrorCodes.NumericValueOutOfRange)
        { return Error(400, "A value exceeds the allowed size."); }
    }
}
