using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace MarketData.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Initial database schema creation
    /// </summary>
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create Users table
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "User"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefreshToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RefreshTokenExpiryTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            // Create SymbolStatistics table
            migrationBuilder.CreateTable(
                name: "SymbolStatistics",
                columns: table => new
                {
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    MovingAverage = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    MinPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    MaxPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    UpdateCount = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SymbolStatistics", x => x.Symbol);
                });

            // Create PriceUpdates table
            migrationBuilder.CreateTable(
                name: "PriceUpdates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceUpdates", x => x.Id);
                });

            // Create PriceAnomalies table
            migrationBuilder.CreateTable(
                name: "PriceAnomalies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OldPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    NewPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    ChangePercent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceAnomalies", x => x.Id);
                });

            // Create indexes for Users
            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive",
                table: "Users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Role",
                table: "Users",
                column: "Role");

            // Create indexes for SymbolStatistics
            migrationBuilder.CreateIndex(
                name: "IX_SymbolStatistics_LastUpdateTime",
                table: "SymbolStatistics",
                column: "LastUpdateTime");

            // Create indexes for PriceUpdates
            migrationBuilder.CreateIndex(
                name: "IX_PriceUpdates_Symbol",
                table: "PriceUpdates",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_PriceUpdates_Timestamp",
                table: "PriceUpdates",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_PriceUpdates_Symbol_Timestamp",
                table: "PriceUpdates",
                columns: new[] { "Symbol", "Timestamp" });

            // Create indexes for PriceAnomalies
            migrationBuilder.CreateIndex(
                name: "IX_PriceAnomalies_Symbol",
                table: "PriceAnomalies",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_PriceAnomalies_DetectedAt",
                table: "PriceAnomalies",
                column: "DetectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PriceAnomalies_Severity",
                table: "PriceAnomalies",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_PriceAnomalies_Symbol_DetectedAt",
                table: "PriceAnomalies",
                columns: new[] { "Symbol", "DetectedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Users");
            migrationBuilder.DropTable(name: "SymbolStatistics");
            migrationBuilder.DropTable(name: "PriceUpdates");
            migrationBuilder.DropTable(name: "PriceAnomalies");
        }
    }
}
