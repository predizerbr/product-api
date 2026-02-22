using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product.Data.Migrations
{
    /// <inheritdoc />
    public partial class portfolio_module : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortfolioSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AsOf = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivePositions = table.Column<int>(type: "integer", nullable: false),
                    TotalInvestedActive = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalInvestedAllTime = table.Column<decimal>(type: "numeric", nullable: false),
                    RealizedPnlAllTime = table.Column<decimal>(type: "numeric", nullable: false),
                    PotentialPnlActive = table.Column<decimal>(type: "numeric", nullable: false),
                    ClosedMarkets = table.Column<int>(type: "integer", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    AccuracyRate = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PositionFills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Side = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Contracts = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    FeeAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionFills", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioSnapshots");

            migrationBuilder.DropTable(
                name: "PositionFills");
        }
    }
}
