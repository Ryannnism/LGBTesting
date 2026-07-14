using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGBApp.Backend.Migrations
{
    /// <inheritdoc />
    public partial class Wave5_FormCustomerIds_Indexes_InvoiceFks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "MOIForms",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "MOIForms",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "MOAForms",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "MOAForms",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_MOIForms_CustomerId",
                table: "MOIForms",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MOAForms_CustomerId",
                table: "MOAForms",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequests_Status",
                table: "JobRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId",
                table: "Invoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_JobRequestId",
                table: "Invoices",
                column: "JobRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CompletedServices_DateCompleted",
                table: "CompletedServices",
                column: "DateCompleted");

            migrationBuilder.CreateIndex(
                name: "IX_CompletedServices_JobRequestId",
                table: "CompletedServices",
                column: "JobRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Customers_CustomerId",
                table: "Invoices",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_JobRequests_JobRequestId",
                table: "Invoices",
                column: "JobRequestId",
                principalTable: "JobRequests",
                principalColumn: "JobRequestId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MOAForms_Customers_CustomerId",
                table: "MOAForms",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MOIForms_Customers_CustomerId",
                table: "MOIForms",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Customers_CustomerId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_JobRequests_JobRequestId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_MOAForms_Customers_CustomerId",
                table: "MOAForms");

            migrationBuilder.DropForeignKey(
                name: "FK_MOIForms_Customers_CustomerId",
                table: "MOIForms");

            migrationBuilder.DropIndex(
                name: "IX_MOIForms_CustomerId",
                table: "MOIForms");

            migrationBuilder.DropIndex(
                name: "IX_MOAForms_CustomerId",
                table: "MOAForms");

            migrationBuilder.DropIndex(
                name: "IX_JobRequests_Status",
                table: "JobRequests");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CustomerId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_JobRequestId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_CompletedServices_DateCompleted",
                table: "CompletedServices");

            migrationBuilder.DropIndex(
                name: "IX_CompletedServices_JobRequestId",
                table: "CompletedServices");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "MOIForms");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "MOIForms");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "MOAForms");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "MOAForms");
        }
    }
}
