using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ByteBait.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "casts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    zone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    strength = table.Column<double>(type: "double precision", nullable: false),
                    cast_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_casts", x => x.id);
                    table.CheckConstraint("ck_casts_strength_range", "strength >= 0 AND strength <= 1");
                    table.CheckConstraint("ck_casts_zone", "zone IN ('red', 'yellow', 'green')");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "casts");
        }
    }
}
