using LGBApp.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGBApp.Backend.Migrations.Postgres;

[DbContext(typeof(AppDbContext))]
[Migration("20260717070000_Pg_CubeVOps")]
public partial class Pg_CubeVOps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RequiredApproverName",
            table: "MOIForms",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "RequiredApproverEmail",
            table: "MOIForms",
            type: "character varying(256)",
            maxLength: 256,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "MoaApproversJson",
            table: "Customers",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "MoaApproversOverrideJson",
            table: "MOAForms",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<DateTime>(
            name: "CompletionNotifiedAt",
            table: "CustomerPackages",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "MoiApprovalMatrixEntries",
            columns: table => new
            {
                MoiApprovalMatrixEntryId = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                GroupCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                RequesterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                RequesterEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                ApproverName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ApproverEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MoiApprovalMatrixEntries", x => x.MoiApprovalMatrixEntryId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MoiApprovalMatrixEntries_RequesterEmail",
            table: "MoiApprovalMatrixEntries",
            column: "RequesterEmail",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MoiApprovalMatrixEntries");
        migrationBuilder.DropColumn(name: "RequiredApproverName", table: "MOIForms");
        migrationBuilder.DropColumn(name: "RequiredApproverEmail", table: "MOIForms");
        migrationBuilder.DropColumn(name: "MoaApproversJson", table: "Customers");
        migrationBuilder.DropColumn(name: "MoaApproversOverrideJson", table: "MOAForms");
        migrationBuilder.DropColumn(name: "CompletionNotifiedAt", table: "CustomerPackages");
    }
}
