using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnalyzerCore.Infrastructure.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 42, nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Decimals = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ChainId = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 42, nullable: false),
                    Token0Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Token1Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Reserve0 = table.Column<decimal>(type: "TEXT", precision: 36, scale: 18, nullable: false),
                    Reserve1 = table.Column<decimal>(type: "TEXT", precision: 36, scale: 18, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Factory = table.Column<string>(type: "TEXT", maxLength: 42, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pools_Tokens_Token0Id",
                        column: x => x.Token0Id,
                        principalTable: "Tokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pools_Tokens_Token1Id",
                        column: x => x.Token1Id,
                        principalTable: "Tokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pools_Address_Factory",
                table: "Pools",
                columns: new[] { "Address", "Factory" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pools_Token0Id",
                table: "Pools",
                column: "Token0Id");

            migrationBuilder.CreateIndex(
                name: "IX_Pools_Token1Id",
                table: "Pools",
                column: "Token1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_Address_ChainId",
                table: "Tokens",
                columns: new[] { "Address", "ChainId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pools");

            migrationBuilder.DropTable(
                name: "Tokens");
        }
    }
}
