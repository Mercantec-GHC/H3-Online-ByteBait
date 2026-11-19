using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ByteBait.Data.Migrations
{
    /// <inheritdoc />
    public partial class GameModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "zone",
                table: "casts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<int>(
                name: "fish_species_id",
                table: "casts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "length_cm",
                table: "casts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "level_id",
                table: "casts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "player_id",
                table: "casts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "weight_kg",
                table: "casts",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "catches",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cast_id = table.Column<int>(type: "integer", nullable: false),
                    result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sold_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sold_for = table.Column<int>(type: "integer", nullable: true),
                    in_aquarium = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catches", x => x.id);
                    table.CheckConstraint("ck_catches_only_caught_is_kept", "result = 'caught' OR (sold_at IS NULL AND NOT in_aquarium)");
                    table.CheckConstraint("ck_catches_result", "result IN ('caught', 'lost')");
                    table.CheckConstraint("ck_catches_sale_complete", "(sold_at IS NULL) = (sold_for IS NULL)");
                    table.CheckConstraint("ck_catches_sold_for_not_negative", "sold_for >= 0");
                    table.CheckConstraint("ck_catches_sold_or_aquarium", "NOT (in_aquarium AND sold_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_catches_casts_cast_id",
                        column: x => x.cast_id,
                        principalTable: "casts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "equipment",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    price = table.Column<int>(type: "integer", nullable: false),
                    power = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment", x => x.id);
                    table.CheckConstraint("ck_equipment_power_not_negative", "power >= 0");
                    table.CheckConstraint("ck_equipment_price_not_negative", "price >= 0");
                    table.CheckConstraint("ck_equipment_type", "type IN ('rod', 'bait', 'line')");
                });

            migrationBuilder.CreateTable(
                name: "lanes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    city = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lanes", x => x.id);
                    table.CheckConstraint("ck_lanes_latitude_range", "latitude >= -90 AND latitude <= 90");
                    table.CheckConstraint("ck_lanes_longitude_range", "longitude >= -180 AND longitude <= 180");
                    table.CheckConstraint("ck_lanes_number_positive", "number >= 1");
                });

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    money = table.Column<int>(type: "integer", nullable: false),
                    aquarium_share_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_players", x => x.id);
                    table.CheckConstraint("ck_players_money_not_negative", "money >= 0");
                });

            migrationBuilder.CreateTable(
                name: "rarity_chances",
                columns: table => new
                {
                    rarity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    base_chance = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rarity_chances", x => x.rarity);
                    table.CheckConstraint("ck_rarity_chances_base_chance_range", "base_chance >= 0 AND base_chance <= 1");
                    table.CheckConstraint("ck_rarity_chances_rarity", "rarity IN ('rare', 'legendary')");
                });

            migrationBuilder.CreateTable(
                name: "zone_multipliers",
                columns: table => new
                {
                    zone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    multiplier = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zone_multipliers", x => x.zone);
                    table.CheckConstraint("ck_zone_multipliers_multiplier_not_negative", "multiplier >= 0");
                    table.CheckConstraint("ck_zone_multipliers_zone", "zone IN ('red', 'yellow', 'green')");
                });

            migrationBuilder.CreateTable(
                name: "fish_species",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lane_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rarity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    depth = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    min_weight_kg = table.Column<double>(type: "double precision", nullable: false),
                    max_weight_kg = table.Column<double>(type: "double precision", nullable: false),
                    min_length_cm = table.Column<double>(type: "double precision", nullable: false),
                    max_length_cm = table.Column<double>(type: "double precision", nullable: false),
                    price = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fish_species", x => x.id);
                    table.CheckConstraint("ck_fish_species_depth", "depth IN ('shallow', 'deep')");
                    table.CheckConstraint("ck_fish_species_length_range", "min_length_cm > 0 AND max_length_cm >= min_length_cm");
                    table.CheckConstraint("ck_fish_species_price_not_negative", "price >= 0");
                    table.CheckConstraint("ck_fish_species_rarity", "rarity IN ('common', 'rare', 'legendary')");
                    table.CheckConstraint("ck_fish_species_weight_range", "min_weight_kg > 0 AND max_weight_kg >= min_weight_kg");
                    table.ForeignKey(
                        name: "fk_fish_species_lanes_lane_id",
                        column: x => x.lane_id,
                        principalTable: "lanes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "levels",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lane_id = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    required_fish = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_levels", x => x.id);
                    table.CheckConstraint("ck_levels_number_range", "number BETWEEN 1 AND 2");
                    table.CheckConstraint("ck_levels_required_fish_positive", "required_fish >= 1");
                    table.ForeignKey(
                        name: "fk_levels_lanes_lane_id",
                        column: x => x.lane_id,
                        principalTable: "lanes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "player_equipment",
                columns: table => new
                {
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_id = table.Column<int>(type: "integer", nullable: false),
                    purchased_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_equipment", x => new { x.player_id, x.equipment_id });
                    table.ForeignKey(
                        name: "fk_player_equipment_equipment_equipment_id",
                        column: x => x.equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_player_equipment_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "rarity_chances",
                columns: new[] { "rarity", "base_chance" },
                values: new object[,]
                {
                    { "legendary", 0.050000000000000003 },
                    { "rare", 0.20000000000000001 }
                });

            migrationBuilder.InsertData(
                table: "zone_multipliers",
                columns: new[] { "zone", "multiplier" },
                values: new object[,]
                {
                    { "green", 1.5 },
                    { "red", 0.0 },
                    { "yellow", 1.0 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_casts_fish_species_id",
                table: "casts",
                column: "fish_species_id");

            migrationBuilder.CreateIndex(
                name: "ix_casts_level_id",
                table: "casts",
                column: "level_id");

            migrationBuilder.CreateIndex(
                name: "ix_casts_player_id",
                table: "casts",
                column: "player_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_casts_fish_complete",
                table: "casts",
                sql: "(fish_species_id IS NULL) = (weight_kg IS NULL) AND (fish_species_id IS NULL) = (length_cm IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_casts_length_positive",
                table: "casts",
                sql: "length_cm > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_casts_weight_positive",
                table: "casts",
                sql: "weight_kg > 0");

            migrationBuilder.CreateIndex(
                name: "ix_catches_cast_id",
                table: "catches",
                column: "cast_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipment_name",
                table: "equipment",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fish_species_lane_id",
                table: "fish_species",
                column: "lane_id");

            migrationBuilder.CreateIndex(
                name: "ix_fish_species_name",
                table: "fish_species",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lanes_number",
                table: "lanes",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_levels_lane_id_number",
                table: "levels",
                columns: new[] { "lane_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_player_equipment_equipment_id",
                table: "player_equipment",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_players_aquarium_share_code",
                table: "players",
                column: "aquarium_share_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_players_user_name",
                table: "players",
                column: "user_name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_casts_fish_species_fish_species_id",
                table: "casts",
                column: "fish_species_id",
                principalTable: "fish_species",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_casts_levels_level_id",
                table: "casts",
                column: "level_id",
                principalTable: "levels",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_casts_players_player_id",
                table: "casts",
                column: "player_id",
                principalTable: "players",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_casts_fish_species_fish_species_id",
                table: "casts");

            migrationBuilder.DropForeignKey(
                name: "fk_casts_levels_level_id",
                table: "casts");

            migrationBuilder.DropForeignKey(
                name: "fk_casts_players_player_id",
                table: "casts");

            migrationBuilder.DropTable(
                name: "catches");

            migrationBuilder.DropTable(
                name: "fish_species");

            migrationBuilder.DropTable(
                name: "levels");

            migrationBuilder.DropTable(
                name: "player_equipment");

            migrationBuilder.DropTable(
                name: "rarity_chances");

            migrationBuilder.DropTable(
                name: "zone_multipliers");

            migrationBuilder.DropTable(
                name: "lanes");

            migrationBuilder.DropTable(
                name: "equipment");

            migrationBuilder.DropTable(
                name: "players");

            migrationBuilder.DropIndex(
                name: "ix_casts_fish_species_id",
                table: "casts");

            migrationBuilder.DropIndex(
                name: "ix_casts_level_id",
                table: "casts");

            migrationBuilder.DropIndex(
                name: "ix_casts_player_id",
                table: "casts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_casts_fish_complete",
                table: "casts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_casts_length_positive",
                table: "casts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_casts_weight_positive",
                table: "casts");

            migrationBuilder.DropColumn(
                name: "fish_species_id",
                table: "casts");

            migrationBuilder.DropColumn(
                name: "length_cm",
                table: "casts");

            migrationBuilder.DropColumn(
                name: "level_id",
                table: "casts");

            migrationBuilder.DropColumn(
                name: "player_id",
                table: "casts");

            migrationBuilder.DropColumn(
                name: "weight_kg",
                table: "casts");

            migrationBuilder.AlterColumn<string>(
                name: "zone",
                table: "casts",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);
        }
    }
}
