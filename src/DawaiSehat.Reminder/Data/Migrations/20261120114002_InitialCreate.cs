using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DawaiSehat.Reminder.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inbound_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    whatsapp_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    from_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    body = table.Column<string>(type: "text", nullable: true),
                    media_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_inbound_messages_whatsapp_message_id",
                table: "inbound_messages",
                column: "whatsapp_message_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbound_messages");
        }
    }
}
