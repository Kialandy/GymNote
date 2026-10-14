using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymNote.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "exercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SuggestedSets = table.Column<int>(type: "integer", nullable: false),
                    SuggestedRepsPerSet = table.Column<int>(type: "integer", nullable: false),
                    SuggestedRestSeconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercises", x => x.Id);
                    table.CheckConstraint("ck_exercises_reps", "\"SuggestedRepsPerSet\" BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_exercises_rest", "\"SuggestedRestSeconds\" BETWEEN 0 AND 600");
                    table.CheckConstraint("ck_exercises_sets", "\"SuggestedSets\" BETWEEN 1 AND 20");
                    table.ForeignKey(
                        name: "FK_exercises_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exercises_UserId",
                table: "exercises",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exercises");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
