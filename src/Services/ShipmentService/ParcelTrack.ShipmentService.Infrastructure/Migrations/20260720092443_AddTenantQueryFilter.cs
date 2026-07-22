using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParcelTrack.ShipmentService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantQueryFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shipment_events_shipments_shipment_id",
                table: "shipment_events");

            migrationBuilder.DropPrimaryKey(
                name: "PK_shipments",
                table: "shipments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_shipment_events",
                table: "shipment_events");

            migrationBuilder.DropPrimaryKey(
                name: "PK_outbox_messages",
                table: "outbox_messages");

            migrationBuilder.AddPrimaryKey(
                name: "pk_shipments",
                table: "shipments",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_shipment_events",
                table: "shipment_events",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_outbox_messages",
                table: "outbox_messages",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_shipment_events_shipments_shipment_id",
                table: "shipment_events",
                column: "shipment_id",
                principalTable: "shipments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_shipment_events_shipments_shipment_id",
                table: "shipment_events");

            migrationBuilder.DropPrimaryKey(
                name: "pk_shipments",
                table: "shipments");

            migrationBuilder.DropPrimaryKey(
                name: "pk_shipment_events",
                table: "shipment_events");

            migrationBuilder.DropPrimaryKey(
                name: "pk_outbox_messages",
                table: "outbox_messages");

            migrationBuilder.AddPrimaryKey(
                name: "PK_shipments",
                table: "shipments",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_shipment_events",
                table: "shipment_events",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_outbox_messages",
                table: "outbox_messages",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_shipment_events_shipments_shipment_id",
                table: "shipment_events",
                column: "shipment_id",
                principalTable: "shipments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
