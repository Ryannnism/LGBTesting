using LGBApp.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGBApp.Backend.Migrations.Postgres;

[DbContext(typeof(AppDbContext))]
[Migration("20260804000000_Pg_LastPointOfApproval")]
public partial class Pg_LastPointOfApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LastPointOfApprovalJson",
            table: "MOIForms",
            type: "text",
            nullable: false,
            defaultValue: "[]");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LastPointOfApprovalJson", table: "MOIForms");
    }
}
