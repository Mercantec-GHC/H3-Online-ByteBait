using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByteBait.Data.Migrations
{
    /// <inheritdoc />
    public partial class SpeciesVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "fish_species",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "fish_species");
        }
    }
}
