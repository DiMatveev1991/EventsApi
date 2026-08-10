using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsApi.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class UseUtcEventTimestamps : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $migration$
            BEGIN
                IF (
                    SELECT COUNT(*)
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'events'
                      AND column_name IN ('StartAt', 'EndAt')
                      AND data_type = 'timestamp without time zone'
                ) = 2
                THEN
                    ALTER TABLE events
                        ALTER COLUMN "StartAt" TYPE timestamp with time zone
                            USING "StartAt" AT TIME ZONE 'UTC',
                        ALTER COLUMN "EndAt" TYPE timestamp with time zone
                            USING "EndAt" AT TIME ZONE 'UTC';
                END IF;
            END
            $migration$;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $migration$
            BEGIN
                IF (
                    SELECT COUNT(*)
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'events'
                      AND column_name IN ('StartAt', 'EndAt')
                      AND data_type = 'timestamp with time zone'
                ) = 2
                THEN
                    ALTER TABLE events
                        ALTER COLUMN "StartAt" TYPE timestamp without time zone
                            USING "StartAt" AT TIME ZONE 'UTC',
                        ALTER COLUMN "EndAt" TYPE timestamp without time zone
                            USING "EndAt" AT TIME ZONE 'UTC';
                END IF;
            END
            $migration$;
            """);
    }
}
