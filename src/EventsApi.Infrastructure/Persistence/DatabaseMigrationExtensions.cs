using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EventsApi.Infrastructure.Persistence;

/// <summary>
/// Совместимый запуск миграций для баз, ранее созданных через EnsureCreated.
/// </summary>
public static class DatabaseMigrationExtensions
{
    private const string LegacyBaselineSql = """
        DO $baseline$
        BEGIN
            IF to_regclass('public.events') IS NOT NULL
               AND to_regclass('public.bookings') IS NOT NULL
               AND (
                   SELECT COUNT(*)
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND (table_name, column_name) IN (
                         ('events', 'Id'),
                         ('events', 'Title'),
                         ('events', 'Description'),
                         ('events', 'StartAt'),
                         ('events', 'EndAt'),
                         ('events', 'TotalSeats'),
                         ('events', 'AvailableSeats'),
                         ('bookings', 'Id'),
                         ('bookings', 'EventId'),
                         ('bookings', 'Status'),
                         ('bookings', 'CreatedAt'),
                         ('bookings', 'ProcessedAt')
                     )
               ) = 12
            THEN
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );

                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('20260710112520_InitialCreate', '8.0.11')
                ON CONFLICT ("MigrationId") DO NOTHING;
            END IF;
        END
        $baseline$;
        """;

    /// <summary>
    /// Регистрирует совместимую legacy-схему как initial migration и применяет
    /// все последующие миграции. Для пустой или уже мигрированной БД работает
    /// так же, как обычный <c>MigrateAsync</c>.
    /// </summary>
    public static async Task MigrateWithLegacyBaselineAsync(
        this DatabaseFacade database,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        await database.ExecuteSqlRawAsync(LegacyBaselineSql, cancellationToken);
        await database.MigrateAsync(cancellationToken);
    }
}
