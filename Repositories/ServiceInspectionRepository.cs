using BoxService_BackEnd.Data;
using BoxService_BackEnd.Models;
using Npgsql;

namespace BoxService_BackEnd.Repositories;

public sealed class ServiceInspectionRepository
{
    private readonly PostgresConnectionFactory _connectionFactory;

    public ServiceInspectionRepository(
        PostgresConnectionFactory connectionFactory
    )
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> ServiceExistsAsync(
        int serviceId,
        CancellationToken cancellationToken
    )
    {
        await using var connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM services
                WHERE id_service = @id_service
            );
            """,
            connection
        );

        command.Parameters.AddWithValue("id_service", serviceId);

        return (bool)(
            await command.ExecuteScalarAsync(cancellationToken) ?? false
        );
    }

    public async Task<ServiceInspection?> GetByServiceIdAsync(
        int serviceId,
        CancellationToken cancellationToken
    )
    {
        await using var connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var inspection = await GetHeaderAsync(
            connection,
            serviceId,
            cancellationToken
        );

        if (inspection is null)
        {
            return null;
        }

        inspection.Items = await GetItemsAsync(
            connection,
            inspection.InspectionId,
            cancellationToken
        );

        inspection.Photos = await GetPhotosAsync(
            connection,
            inspection.InspectionId,
            cancellationToken
        );

        return inspection;
    }

    public async Task<ServiceInspection> CreateAsync(
        ServiceInspection inspection,
        CancellationToken cancellationToken
    )
    {
        await using var connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        const string sql = """
            INSERT INTO service_inspections (
                id_service,
                fuel_level,
                general_observations,
                inspected_by
            )
            VALUES (
                @id_service,
                @fuel_level,
                @general_observations,
                @inspected_by
            )
            RETURNING id_inspection, inspected_at;
            """;

        await using (var command =
            new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue(
                "id_service",
                inspection.ServiceId
            );

            command.Parameters.AddWithValue(
                "fuel_level",
                (object?)inspection.FuelLevel ?? DBNull.Value
            );

            command.Parameters.AddWithValue(
                "general_observations",
                string.IsNullOrEmpty(inspection.GeneralObservations)
                    ? DBNull.Value
                    : inspection.GeneralObservations
            );

            command.Parameters.AddWithValue(
                "inspected_by",
                inspection.InspectedBy
            );

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            await reader.ReadAsync(cancellationToken);

            inspection.InspectionId = reader.GetInt32(0);
            inspection.InspectedAt = reader.GetDateTime(1);
        }

        await InsertItemsAsync(
            connection,
            transaction,
            inspection,
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        return inspection;
    }

    public async Task<ServiceInspection?> UpdateAsync(
        ServiceInspection inspection,
        CancellationToken cancellationToken
    )
    {
        await using var connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE service_inspections
            SET fuel_level = @fuel_level,
                general_observations = @general_observations,
                inspected_by = @inspected_by,
                inspected_at = NOW()
            WHERE id_service = @id_service
            RETURNING id_inspection, inspected_at;
            """;

        await using (var command =
            new NpgsqlCommand(updateSql, connection, transaction))
        {
            command.Parameters.AddWithValue(
                "id_service",
                inspection.ServiceId
            );

            command.Parameters.AddWithValue(
                "fuel_level",
                (object?)inspection.FuelLevel ?? DBNull.Value
            );

            command.Parameters.AddWithValue(
                "general_observations",
                string.IsNullOrEmpty(inspection.GeneralObservations)
                    ? DBNull.Value
                    : inspection.GeneralObservations
            );

            command.Parameters.AddWithValue(
                "inspected_by",
                inspection.InspectedBy
            );

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            inspection.InspectionId = reader.GetInt32(0);
            inspection.InspectedAt = reader.GetDateTime(1);
        }

        await using (var deleteItems = new NpgsqlCommand(
            """
            DELETE FROM service_inspection_items
            WHERE id_inspection = @id_inspection;
            """,
            connection,
            transaction
        ))
        {
            deleteItems.Parameters.AddWithValue(
                "id_inspection",
                inspection.InspectionId
            );

            await deleteItems.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertItemsAsync(
            connection,
            transaction,
            inspection,
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        return inspection;
    }

    public async Task<int> CountPhotosAsync(
        int inspectionId,
        CancellationToken cancellationToken
    )
    {
        await using var connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            SELECT COUNT(*)
            FROM service_inspection_photos
            WHERE id_inspection = @id_inspection;
            """,
            connection
        );

        command.Parameters.AddWithValue(
            "id_inspection",
            inspectionId
        );

        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken)
        );
    }

    public async Task<ServiceInspectionPhoto> CreatePhotoAsync(
        ServiceInspectionPhoto photo,
        CancellationToken cancellationToken
    )
    {
        const string sql = """
            INSERT INTO service_inspection_photos (
                id_inspection,
                storage_path,
                photo_type,
                description,
                original_file_name,
                content_type
            )
            VALUES (
                @id_inspection,
                @storage_path,
                @photo_type,
                @description,
                @original_file_name,
                @content_type
            )
            RETURNING id_photo, created_at;
            """;

        await using var connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "id_inspection",
            photo.InspectionId
        );

        command.Parameters.AddWithValue(
            "storage_path",
            photo.StoragePath
        );

        command.Parameters.AddWithValue(
            "photo_type",
            photo.PhotoType
        );

        command.Parameters.AddWithValue(
            "description",
            string.IsNullOrEmpty(photo.Description)
                ? DBNull.Value
                : photo.Description
        );

        command.Parameters.AddWithValue(
            "original_file_name",
            photo.OriginalFileName
        );

        command.Parameters.AddWithValue(
            "content_type",
            photo.ContentType
        );

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        await reader.ReadAsync(cancellationToken);

        photo.PhotoId = reader.GetInt32(0);
        photo.CreatedAt = reader.GetDateTime(1);

        return photo;
    }

    public async Task<ServiceInspectionPhoto?> GetPhotoAsync(
        int serviceId,
        int photoId,
        CancellationToken cancellationToken
    )
    {
        const string sql = """
            SELECT
                p.id_photo,
                p.id_inspection,
                p.storage_path,
                p.photo_type,
                p.description,
                p.original_file_name,
                p.content_type,
                p.created_at
            FROM service_inspection_photos p
            INNER JOIN service_inspections i
                ON i.id_inspection = p.id_inspection
            WHERE i.id_service = @id_service
              AND p.id_photo = @id_photo;
            """;

        await using var connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "id_service",
            serviceId
        );

        command.Parameters.AddWithValue(
            "id_photo",
            photoId
        );

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ServiceInspectionPhoto
        {
            PhotoId = reader.GetInt32(
                reader.GetOrdinal("id_photo")
            ),

            InspectionId = reader.GetInt32(
                reader.GetOrdinal("id_inspection")
            ),

            StoragePath = reader.GetString(
                reader.GetOrdinal("storage_path")
            ),

            PhotoType = reader.GetString(
                reader.GetOrdinal("photo_type")
            ),

            Description = reader.IsDBNull(
                reader.GetOrdinal("description")
            )
                ? string.Empty
                : reader.GetString(
                    reader.GetOrdinal("description")
                ),

            OriginalFileName = reader.GetString(
                reader.GetOrdinal("original_file_name")
            ),

            ContentType = reader.GetString(
                reader.GetOrdinal("content_type")
            ),

            CreatedAt = reader.GetDateTime(
                reader.GetOrdinal("created_at")
            )
        };
    }

    public async Task<bool> DeletePhotoAsync(
        int photoId,
        CancellationToken cancellationToken
    )
    {
        await using var connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            """
            DELETE FROM service_inspection_photos
            WHERE id_photo = @id_photo;
            """,
            connection
        );

        command.Parameters.AddWithValue(
            "id_photo",
            photoId
        );

        return await command.ExecuteNonQueryAsync(
            cancellationToken
        ) > 0;
    }

    private static async Task<ServiceInspection?> GetHeaderAsync(
        NpgsqlConnection connection,
        int serviceId,
        CancellationToken cancellationToken
    )
    {
        const string sql = """
            SELECT
                id_inspection,
                id_service,
                fuel_level,
                general_observations,
                inspected_by,
                inspected_at
            FROM service_inspections
            WHERE id_service = @id_service;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "id_service",
            serviceId
        );

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ServiceInspection
        {
            InspectionId = reader.GetInt32(
                reader.GetOrdinal("id_inspection")
            ),

            ServiceId = reader.GetInt32(
                reader.GetOrdinal("id_service")
            ),

            FuelLevel = reader.IsDBNull(
                reader.GetOrdinal("fuel_level")
            )
                ? null
                : reader.GetInt16(
                    reader.GetOrdinal("fuel_level")
                ),

            GeneralObservations = reader.IsDBNull(
                reader.GetOrdinal("general_observations")
            )
                ? string.Empty
                : reader.GetString(
                    reader.GetOrdinal("general_observations")
                ),

            InspectedBy = reader.GetString(
                reader.GetOrdinal("inspected_by")
            ),

            InspectedAt = reader.GetDateTime(
                reader.GetOrdinal("inspected_at")
            )
        };
    }

    private static async Task<List<ServiceInspectionItem>> GetItemsAsync(
        NpgsqlConnection connection,
        int inspectionId,
        CancellationToken cancellationToken
    )
    {
        const string sql = """
            SELECT
                id_item,
                id_inspection,
                item_code,
                item_name,
                status,
                observation
            FROM service_inspection_items
            WHERE id_inspection = @id_inspection
            ORDER BY id_item;
            """;

        var items = new List<ServiceInspectionItem>();

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "id_inspection",
            inspectionId
        );

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ServiceInspectionItem
            {
                ItemId = reader.GetInt32(
                    reader.GetOrdinal("id_item")
                ),

                InspectionId = reader.GetInt32(
                    reader.GetOrdinal("id_inspection")
                ),

                ItemCode = reader.GetString(
                    reader.GetOrdinal("item_code")
                ),

                ItemName = reader.GetString(
                    reader.GetOrdinal("item_name")
                ),

                Status = reader.GetString(
                    reader.GetOrdinal("status")
                ),

                Observation = reader.IsDBNull(
                    reader.GetOrdinal("observation")
                )
                    ? string.Empty
                    : reader.GetString(
                        reader.GetOrdinal("observation")
                    )
            });
        }

        return items;
    }

    private static async Task<List<ServiceInspectionPhoto>> GetPhotosAsync(
        NpgsqlConnection connection,
        int inspectionId,
        CancellationToken cancellationToken
    )
    {
        const string sql = """
            SELECT
                id_photo,
                id_inspection,
                storage_path,
                photo_type,
                description,
                original_file_name,
                content_type,
                created_at
            FROM service_inspection_photos
            WHERE id_inspection = @id_inspection
            ORDER BY id_photo;
            """;

        var photos = new List<ServiceInspectionPhoto>();

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "id_inspection",
            inspectionId
        );

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            photos.Add(new ServiceInspectionPhoto
            {
                PhotoId = reader.GetInt32(
                    reader.GetOrdinal("id_photo")
                ),

                InspectionId = reader.GetInt32(
                    reader.GetOrdinal("id_inspection")
                ),

                StoragePath = reader.GetString(
                    reader.GetOrdinal("storage_path")
                ),

                PhotoType = reader.GetString(
                    reader.GetOrdinal("photo_type")
                ),

                Description = reader.IsDBNull(
                    reader.GetOrdinal("description")
                )
                    ? string.Empty
                    : reader.GetString(
                        reader.GetOrdinal("description")
                    ),

                OriginalFileName = reader.GetString(
                    reader.GetOrdinal("original_file_name")
                ),

                ContentType = reader.GetString(
                    reader.GetOrdinal("content_type")
                ),

                CreatedAt = reader.GetDateTime(
                    reader.GetOrdinal("created_at")
                )
            });
        }

        return photos;
    }

    private static async Task InsertItemsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ServiceInspection inspection,
        CancellationToken cancellationToken
    )
    {
        const string sql = """
            INSERT INTO service_inspection_items (
                id_inspection,
                item_code,
                item_name,
                status,
                observation
            )
            VALUES (
                @id_inspection,
                @item_code,
                @item_name,
                @status,
                @observation
            )
            RETURNING id_item;
            """;

        foreach (var item in inspection.Items)
        {
            await using var command =
                new NpgsqlCommand(sql, connection, transaction);

            command.Parameters.AddWithValue(
                "id_inspection",
                inspection.InspectionId
            );

            command.Parameters.AddWithValue(
                "item_code",
                item.ItemCode
            );

            command.Parameters.AddWithValue(
                "item_name",
                item.ItemName
            );

            command.Parameters.AddWithValue(
                "status",
                item.Status
            );

            command.Parameters.AddWithValue(
                "observation",
                string.IsNullOrEmpty(item.Observation)
                    ? DBNull.Value
                    : item.Observation
            );

            item.ItemId = Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken)
            );

            item.InspectionId = inspection.InspectionId;
        }
    }
}
