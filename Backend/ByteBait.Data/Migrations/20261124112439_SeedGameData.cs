using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ByteBait.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedGameData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fish_species_lane_id",
                table: "fish_species");

            migrationBuilder.DropIndex(
                name: "ix_fish_species_name",
                table: "fish_species");

            migrationBuilder.InsertData(
                table: "equipment",
                columns: new[] { "id", "name", "power", "price", "type" },
                values: new object[,]
                {
                    { 1, "Carbon Rod", 1, 200, "rod" },
                    { 2, "Nightcrawler", 1, 50, "bait" },
                    { 3, "Braided Line", 1, 150, "line" }
                });

            migrationBuilder.InsertData(
                table: "lanes",
                columns: new[] { "id", "city", "latitude", "longitude", "name", "number" },
                values: new object[,]
                {
                    { 1, "Nordborg", 55.060000000000002, 9.7400000000000002, "Nordborg Sø", 1 },
                    { 2, "Silkeborg", 56.170000000000002, 9.5500000000000007, "Gudenåen", 2 },
                    { 3, "Ringkøbing", 56.090000000000003, 8.2400000000000002, "Ringkøbing Havn", 3 },
                    { 4, "Vejle", 55.710000000000001, 9.5399999999999991, "Open sea off Vejle", 4 }
                });

            migrationBuilder.InsertData(
                table: "fish_species",
                columns: new[] { "id", "depth", "lane_id", "max_length_cm", "max_weight_kg", "min_length_cm", "min_weight_kg", "name", "price", "rarity" },
                values: new object[,]
                {
                    { 1, "shallow", 1, 40.0, 1.2, 15.0, 0.10000000000000001, "Perch", 10, "common" },
                    { 2, "shallow", 1, 60.0, 3.5, 30.0, 0.80000000000000004, "Tench", 30, "rare" },
                    { 3, "shallow", 1, 120.0, 14.0, 60.0, 3.0, "Pike", 100, "legendary" },
                    { 4, "deep", 1, 60.0, 3.5, 25.0, 0.5, "Bream", 12, "common" },
                    { 5, "deep", 1, 85.0, 7.0, 40.0, 1.0, "Zander", 35, "rare" },
                    { 6, "deep", 1, 200.0, 45.0, 100.0, 10.0, "Wels Catfish", 120, "legendary" },
                    { 7, "shallow", 2, 48.0, 1.5, 25.0, 0.29999999999999999, "Grayling", 18, "common" },
                    { 8, "shallow", 2, 65.0, 4.0, 30.0, 0.5, "Trout", 50, "rare" },
                    { 9, "shallow", 2, 95.0, 11.0, 60.0, 3.0, "Sea Trout", 150, "legendary" },
                    { 10, "deep", 2, 45.0, 1.6000000000000001, 20.0, 0.29999999999999999, "Perch", 20, "common" },
                    { 11, "deep", 2, 110.0, 10.0, 50.0, 2.0, "Pike", 60, "rare" },
                    { 12, "deep", 2, 120.0, 18.0, 75.0, 5.0, "Salmon", 180, "legendary" },
                    { 13, "shallow", 3, 45.0, 1.5, 20.0, 0.29999999999999999, "Flounder", 25, "common" },
                    { 14, "shallow", 3, 90.0, 1.3, 50.0, 0.40000000000000002, "Garfish", 70, "rare" },
                    { 15, "shallow", 3, 80.0, 12.0, 40.0, 2.0, "Turbot", 220, "legendary" },
                    { 16, "deep", 3, 32.0, 0.40000000000000002, 18.0, 0.10000000000000001, "Herring", 28, "common" },
                    { 17, "deep", 3, 80.0, 6.0, 40.0, 1.0, "Sea Bass", 80, "rare" },
                    { 18, "deep", 3, 220.0, 35.0, 120.0, 8.0, "Conger Eel", 250, "legendary" },
                    { 19, "shallow", 4, 45.0, 1.2, 25.0, 0.29999999999999999, "Mackerel", 35, "common" },
                    { 20, "shallow", 4, 85.0, 8.0, 45.0, 1.5, "Sea Trout", 100, "rare" },
                    { 21, "shallow", 4, 280.0, 120.0, 150.0, 30.0, "Swordfish", 320, "legendary" },
                    { 22, "deep", 4, 90.0, 8.0, 35.0, 0.80000000000000004, "Cod", 40, "common" },
                    { 23, "deep", 4, 150.0, 18.0, 70.0, 3.0, "Ling", 120, "rare" },
                    { 24, "deep", 4, 290.0, 350.0, 160.0, 80.0, "Bluefin Tuna", 400, "legendary" }
                });

            migrationBuilder.InsertData(
                table: "levels",
                columns: new[] { "id", "lane_id", "number", "required_fish" },
                values: new object[,]
                {
                    { 1, 1, 1, 5 },
                    { 2, 1, 2, 5 },
                    { 3, 2, 1, 7 },
                    { 4, 2, 2, 8 },
                    { 5, 3, 1, 10 },
                    { 6, 3, 2, 10 },
                    { 7, 4, 1, 12 },
                    { 8, 4, 2, 13 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_fish_species_lane_id_name",
                table: "fish_species",
                columns: new[] { "lane_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fish_species_lane_id_name",
                table: "fish_species");

            migrationBuilder.DeleteData(
                table: "equipment",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "equipment",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "equipment",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "fish_species",
                keyColumn: "id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "levels",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "levels",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "levels",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "levels",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "levels",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "levels",
                keyColumn: "id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "levels",
                keyColumn: "id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "levels",
                keyColumn: "id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "lanes",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "lanes",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "lanes",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "lanes",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.CreateIndex(
                name: "ix_fish_species_lane_id",
                table: "fish_species",
                column: "lane_id");

            migrationBuilder.CreateIndex(
                name: "ix_fish_species_name",
                table: "fish_species",
                column: "name",
                unique: true);
        }
    }
}
