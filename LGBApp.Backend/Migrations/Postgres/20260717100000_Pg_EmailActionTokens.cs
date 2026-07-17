using LGBApp.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGBApp.Backend.Migrations.Postgres;

[DbContext(typeof(AppDbContext))]
[Migration("20260717100000_Pg_EmailActionTokens")]
public partial class Pg_EmailActionTokens : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApprovalActionTokens",
            columns: table => new
            {
                ApprovalActionTokenId = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                WorkflowStepInstanceId = table.Column<int>(type: "integer", nullable: false),
                MoaFormId = table.Column<int>(type: "integer", nullable: false),
                AssigneeUserId = table.Column<int>(type: "integer", nullable: true),
                AssigneeEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                AssigneeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApprovalActionTokens", x => x.ApprovalActionTokenId);
                table.ForeignKey(
                    name: "FK_ApprovalActionTokens_WorkflowStepInstances_WorkflowStepInstanceId",
                    column: x => x.WorkflowStepInstanceId,
                    principalTable: "WorkflowStepInstances",
                    principalColumn: "WorkflowStepInstanceId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalActionTokens_TokenHash",
            table: "ApprovalActionTokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApprovalActionTokens_WorkflowStepInstanceId",
            table: "ApprovalActionTokens",
            column: "WorkflowStepInstanceId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ApprovalActionTokens");
    }
}
