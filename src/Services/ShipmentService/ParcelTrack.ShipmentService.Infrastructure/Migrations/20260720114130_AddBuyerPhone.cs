using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParcelTrack.ShipmentService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyerPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "buyer_phone",
                table: "shipments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "buyer_phone",
                table: "shipments");
        }
    }
}
