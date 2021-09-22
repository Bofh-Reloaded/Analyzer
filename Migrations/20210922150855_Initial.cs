using Microsoft.EntityFrameworkCore.Migrations;

namespace AnalyzerCore.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tokens",
                columns: table => new
                {
                    TokenId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TokenAddress = table.Column<string>(type: "TEXT", nullable: true),
                    TokenSymbol = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeflationary = table.Column<bool>(type: "INTEGER", nullable: false),
                    TxCount = table.Column<int>(type: "INTEGER", nullable: false),
                    From = table.Column<string>(type: "TEXT", nullable: true),
                    To = table.Column<string>(type: "TEXT", nullable: true),
                    TelegramMsgId = table.Column<int>(type: "INTEGER", nullable: false),
                    Notified = table.Column<bool>(type: "INTEGER", nullable: false),
                    Deleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tokens", x => x.TokenId);
                });

            migrationBuilder.CreateTable(
                name: "Exchanges",
                columns: table => new
                {
                    ExchangeId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    TokenEntityTokenId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exchanges", x => x.ExchangeId);
                    table.ForeignKey(
                        name: "FK_Exchanges_Tokens_TokenEntityTokenId",
                        column: x => x.TokenEntityTokenId,
                        principalTable: "Tokens",
                        principalColumn: "TokenId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pools",
                columns: table => new
                {
                    PoolId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    TokenEntityTokenId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pools", x => x.PoolId);
                    table.ForeignKey(
                        name: "FK_Pools_Tokens_TokenEntityTokenId",
                        column: x => x.TokenEntityTokenId,
                        principalTable: "Tokens",
                        principalColumn: "TokenId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransactionHashes",
                columns: table => new
                {
                    HashId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Hash = table.Column<string>(type: "TEXT", nullable: true),
                    TokenEntityTokenId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionHashes", x => x.HashId);
                    table.ForeignKey(
                        name: "FK_TransactionHashes_Tokens_TokenEntityTokenId",
                        column: x => x.TokenEntityTokenId,
                        principalTable: "Tokens",
                        principalColumn: "TokenId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Exchanges_TokenEntityTokenId",
                table: "Exchanges",
                column: "TokenEntityTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_Pools_TokenEntityTokenId",
                table: "Pools",
                column: "TokenEntityTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionHashes_TokenEntityTokenId",
                table: "TransactionHashes",
                column: "TokenEntityTokenId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Exchanges");

            migrationBuilder.DropTable(
                name: "Pools");

            migrationBuilder.DropTable(
                name: "TransactionHashes");

            migrationBuilder.DropTable(
                name: "Tokens");
        }
    }
}
