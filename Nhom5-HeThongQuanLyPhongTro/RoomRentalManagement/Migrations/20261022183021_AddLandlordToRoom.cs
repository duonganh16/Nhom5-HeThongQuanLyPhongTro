using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomRentalManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddLandlordToRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LandlordID",
                table: "Rooms",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_LandlordID",
                table: "Rooms",
                column: "LandlordID");

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_Users_LandlordID",
                table: "Rooms",
                column: "LandlordID",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_Users_LandlordID",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_LandlordID",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "LandlordID",
                table: "Rooms");
        }
    }
}
