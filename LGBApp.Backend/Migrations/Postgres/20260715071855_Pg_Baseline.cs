using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LGBApp.Backend.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class Pg_Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppNotifications",
                columns: table => new
                {
                    AppNotificationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    JobRequestId = table.Column<int>(type: "integer", nullable: true),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppNotifications", x => x.AppNotificationId);
                });

            migrationBuilder.CreateTable(
                name: "BillingParties",
                columns: table => new
                {
                    BillingPartyId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingParties", x => x.BillingPartyId);
                });

            migrationBuilder.CreateTable(
                name: "CompletedServices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobRequestId = table.Column<int>(type: "integer", nullable: true),
                    Customer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Service = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UsedQty = table.Column<int>(type: "integer", nullable: false),
                    TotalQty = table.Column<int>(type: "integer", nullable: false),
                    DateRequested = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateCompleted = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AccountHolder = table.Column<string>(type: "text", nullable: false),
                    JobAssignedTo = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompletedServices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    CustomerId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false),
                    LastContact = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InvoiceBy = table.Column<string>(type: "text", nullable: false),
                    ChargeTo = table.Column<string>(type: "text", nullable: false),
                    InvoiceByPartyIdsJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    ChargeToPartyIdsJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    Package = table.Column<string>(type: "text", nullable: false),
                    PackageValue = table.Column<decimal>(type: "numeric", nullable: false),
                    Cosec = table.Column<bool>(type: "boolean", nullable: false),
                    DivisionGroupCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HasLoa = table.Column<bool>(type: "boolean", nullable: false),
                    LoaHoldersJson = table.Column<string>(type: "text", nullable: false),
                    MoiFormTemplateCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MoaFormTemplateCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MoaWorkflowTemplateCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MoiJson = table.Column<string>(type: "text", nullable: false),
                    MoiApprovalJson = table.Column<string>(type: "text", nullable: false),
                    MoaJson = table.Column<string>(type: "text", nullable: false),
                    MoiApprovalMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "AllRequired"),
                    PurchasedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.CustomerId);
                });

            migrationBuilder.CreateTable(
                name: "DivisionGroups",
                columns: table => new
                {
                    DivisionGroupId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MoaWorkflowTemplateCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DefaultMoiFormTemplateCode = table.Column<string>(type: "text", nullable: true),
                    DefaultMoaFormTemplateCode = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivisionGroups", x => x.DivisionGroupId);
                });

            migrationBuilder.CreateTable(
                name: "FormTemplates",
                columns: table => new
                {
                    FormTemplateId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FormType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    AddressedTo = table.Column<string>(type: "text", nullable: false),
                    DivisionLabel = table.Column<string>(type: "text", nullable: false),
                    IssuerEntity = table.Column<string>(type: "text", nullable: false),
                    PackageServiceName = table.Column<string>(type: "text", nullable: false),
                    FieldsJson = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormTemplates", x => x.FormTemplateId);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetOtps",
                columns: table => new
                {
                    PasswordResetOtpId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetOtps", x => x.PasswordResetOtpId);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PackageName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ServicesJson = table.Column<string>(type: "text", nullable: false),
                    ServiceQuantitiesJson = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QtyPerYear = table.Column<int>(type: "integer", nullable: false),
                    PackagePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    AddOnsJson = table.Column<string>(type: "text", nullable: false),
                    AddOnQuantitiesJson = table.Column<string>(type: "text", nullable: false),
                    AddOnsQty = table.Column<int>(type: "integer", nullable: false),
                    AddOnPrice = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.ProductId);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTemplates",
                columns: table => new
                {
                    WorkflowTemplateId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    WorkflowType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTemplates", x => x.WorkflowTemplateId);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPackages",
                columns: table => new
                {
                    CustomerPackageId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    PackageName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PackageValue = table.Column<decimal>(type: "numeric", nullable: false),
                    PackageDetail = table.Column<string>(type: "text", nullable: true),
                    Validity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PricingJson = table.Column<string>(type: "text", nullable: false),
                    PurchasedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPackages", x => x.CustomerPackageId);
                    table.ForeignKey(
                        name: "FK_CustomerPackages_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "User"),
                    JobTitle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CanRecommendMoi = table.Column<bool>(type: "boolean", nullable: false),
                    CanApproveMoiIntake = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CanApproveMoi = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CanApproveMoa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsInternalSignatory = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    InvitedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Users_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepTemplates",
                columns: table => new
                {
                    WorkflowStepTemplateId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkflowTemplateId = table.Column<int>(type: "integer", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    StepKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ConditionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssigneeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssigneeRole = table.Column<string>(type: "text", nullable: true),
                    AssigneeUserId = table.Column<int>(type: "integer", nullable: true),
                    AssigneeDisplayName = table.Column<string>(type: "text", nullable: true),
                    AllowAdminOverride = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepTemplates", x => x.WorkflowStepTemplateId);
                    table.ForeignKey(
                        name: "FK_WorkflowStepTemplates_WorkflowTemplates_WorkflowTemplateId",
                        column: x => x.WorkflowTemplateId,
                        principalTable: "WorkflowTemplates",
                        principalColumn: "WorkflowTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobRequests",
                columns: table => new
                {
                    JobRequestId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    CustomerPackageId = table.Column<int>(type: "integer", nullable: true),
                    Customer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaskType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Service = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UsedQty = table.Column<int>(type: "integer", nullable: false),
                    TotalQty = table.Column<int>(type: "integer", nullable: false),
                    DateRequested = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateCompleted = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccountHolder = table.Column<string>(type: "text", nullable: false),
                    AccountHolderEmail = table.Column<string>(type: "text", nullable: false),
                    AccountHolderPhone = table.Column<string>(type: "text", nullable: false),
                    AssignedUserId = table.Column<int>(type: "integer", nullable: true),
                    JobAssignedTo = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InternalHandoffStatus = table.Column<string>(type: "text", nullable: false),
                    AssignmentComments = table.Column<string>(type: "text", nullable: true),
                    WorkflowMode = table.Column<string>(type: "text", nullable: false),
                    AdminBypassNote = table.Column<string>(type: "text", nullable: false),
                    AdminBypassAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdminBypassByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequests", x => x.JobRequestId);
                    table.ForeignKey(
                        name: "FK_JobRequests_CustomerPackages_CustomerPackageId",
                        column: x => x.CustomerPackageId,
                        principalTable: "CustomerPackages",
                        principalColumn: "CustomerPackageId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobRequests_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PackageScheduleItems",
                columns: table => new
                {
                    PackageScheduleItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    CustomerPackageId = table.Column<int>(type: "integer", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    BookingUrl = table.Column<string>(type: "text", nullable: true),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: true),
                    JobRequestUnitId = table.Column<int>(type: "integer", nullable: true),
                    AssignedUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageScheduleItems", x => x.PackageScheduleItemId);
                    table.ForeignKey(
                        name: "FK_PackageScheduleItems_CustomerPackages_CustomerPackageId",
                        column: x => x.CustomerPackageId,
                        principalTable: "CustomerPackages",
                        principalColumn: "CustomerPackageId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageScheduleItems_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountHolders",
                columns: table => new
                {
                    AccountHolderId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NeedsMoi = table.Column<bool>(type: "boolean", nullable: false),
                    NeedsMoiApproval = table.Column<bool>(type: "boolean", nullable: false),
                    NeedsMoa = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    ClientAdded = table.Column<bool>(type: "boolean", nullable: false),
                    AddedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountHolders", x => x.AccountHolderId);
                    table.ForeignKey(
                        name: "FK_AccountHolders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountHolders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DivisionGroupRecommenders",
                columns: table => new
                {
                    DivisionGroupRecommenderId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DivisionGroupId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivisionGroupRecommenders", x => x.DivisionGroupRecommenderId);
                    table.ForeignKey(
                        name: "FK_DivisionGroupRecommenders_DivisionGroups_DivisionGroupId",
                        column: x => x.DivisionGroupId,
                        principalTable: "DivisionGroups",
                        principalColumn: "DivisionGroupId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DivisionGroupRecommenders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SignatoryCustomerAccess",
                columns: table => new
                {
                    SignatoryCustomerAccessId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatoryCustomerAccess", x => x.SignatoryCustomerAccessId);
                    table.ForeignKey(
                        name: "FK_SignatoryCustomerAccess_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SignatoryCustomerAccess_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    InvoiceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    JobRequestId = table.Column<int>(type: "integer", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.InvoiceId);
                    table.ForeignKey(
                        name: "FK_Invoices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_JobRequests_JobRequestId",
                        column: x => x.JobRequestId,
                        principalTable: "JobRequests",
                        principalColumn: "JobRequestId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "JobRequestUnits",
                columns: table => new
                {
                    JobRequestUnitId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobRequestId = table.Column<int>(type: "integer", nullable: false),
                    UnitNumber = table.Column<int>(type: "integer", nullable: false),
                    AssignedUserId = table.Column<int>(type: "integer", nullable: true),
                    AssignedUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InternalHandoffStatus = table.Column<string>(type: "text", nullable: false),
                    WorkflowMode = table.Column<string>(type: "text", nullable: false),
                    AdminBypassNote = table.Column<string>(type: "text", nullable: false),
                    AdminBypassAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdminBypassByUserId = table.Column<int>(type: "integer", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PackageScheduleItemId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequestUnits", x => x.JobRequestUnitId);
                    table.ForeignKey(
                        name: "FK_JobRequestUnits_JobRequests_JobRequestId",
                        column: x => x.JobRequestId,
                        principalTable: "JobRequests",
                        principalColumn: "JobRequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceJobForms",
                columns: table => new
                {
                    ServiceJobFormId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobRequestId = table.Column<int>(type: "integer", nullable: false),
                    Company = table.Column<string>(type: "text", nullable: false),
                    Service = table.Column<string>(type: "text", nullable: false),
                    FormDataJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceJobForms", x => x.ServiceJobFormId);
                    table.ForeignKey(
                        name: "FK_ServiceJobForms_JobRequests_JobRequestId",
                        column: x => x.JobRequestId,
                        principalTable: "JobRequests",
                        principalColumn: "JobRequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobItemDocuments",
                columns: table => new
                {
                    JobItemDocumentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobRequestId = table.Column<int>(type: "integer", nullable: false),
                    JobRequestUnitId = table.Column<int>(type: "integer", nullable: true),
                    Folder = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    UploadedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VisibleToInternal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobItemDocuments", x => x.JobItemDocumentId);
                    table.ForeignKey(
                        name: "FK_JobItemDocuments_JobRequestUnits_JobRequestUnitId",
                        column: x => x.JobRequestUnitId,
                        principalTable: "JobRequestUnits",
                        principalColumn: "JobRequestUnitId");
                    table.ForeignKey(
                        name: "FK_JobItemDocuments_JobRequests_JobRequestId",
                        column: x => x.JobRequestId,
                        principalTable: "JobRequests",
                        principalColumn: "JobRequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobRequestUnitAssignees",
                columns: table => new
                {
                    JobRequestUnitAssigneeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobRequestUnitId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequestUnitAssignees", x => x.JobRequestUnitAssigneeId);
                    table.ForeignKey(
                        name: "FK_JobRequestUnitAssignees_JobRequestUnits_JobRequestUnitId",
                        column: x => x.JobRequestUnitId,
                        principalTable: "JobRequestUnits",
                        principalColumn: "JobRequestUnitId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobRequestUnitAssignees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MOIForms",
                columns: table => new
                {
                    MOIFormId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobRequestId = table.Column<int>(type: "integer", nullable: true),
                    JobRequestUnitId = table.Column<int>(type: "integer", nullable: true),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Company = table.Column<string>(type: "text", nullable: false),
                    FormDataJson = table.Column<string>(type: "text", nullable: false),
                    FormTemplateCode = table.Column<string>(type: "text", nullable: false),
                    WorkflowState = table.Column<string>(type: "text", nullable: false),
                    FinanceRelated = table.Column<bool>(type: "boolean", nullable: false),
                    BankSignatoryMatter = table.Column<bool>(type: "boolean", nullable: false),
                    RecommendedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RecommendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecommendationComments = table.Column<string>(type: "text", nullable: false),
                    ClientApprovalsJson = table.Column<string>(type: "text", nullable: false),
                    RejectionsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MOIForms", x => x.MOIFormId);
                    table.ForeignKey(
                        name: "FK_MOIForms_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MOIForms_JobRequestUnits_JobRequestUnitId",
                        column: x => x.JobRequestUnitId,
                        principalTable: "JobRequestUnits",
                        principalColumn: "JobRequestUnitId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MOIForms_JobRequests_JobRequestId",
                        column: x => x.JobRequestId,
                        principalTable: "JobRequests",
                        principalColumn: "JobRequestId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MOAForms",
                columns: table => new
                {
                    MOAFormId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobRequestId = table.Column<int>(type: "integer", nullable: true),
                    JobRequestUnitId = table.Column<int>(type: "integer", nullable: true),
                    MOIFormId = table.Column<int>(type: "integer", nullable: true),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Company = table.Column<string>(type: "text", nullable: false),
                    FormDataJson = table.Column<string>(type: "text", nullable: false),
                    FormTemplateCode = table.Column<string>(type: "text", nullable: false),
                    FinanceRelated = table.Column<bool>(type: "boolean", nullable: false),
                    BankSignatoryMatter = table.Column<bool>(type: "boolean", nullable: false),
                    ShareMovement = table.Column<bool>(type: "boolean", nullable: false),
                    PackChecklistJson = table.Column<string>(type: "text", nullable: false),
                    ClientApprovalsJson = table.Column<string>(type: "text", nullable: false),
                    RejectionsJson = table.Column<string>(type: "text", nullable: false),
                    SharonApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedForAdminReviewAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MOAForms", x => x.MOAFormId);
                    table.ForeignKey(
                        name: "FK_MOAForms_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MOAForms_JobRequestUnits_JobRequestUnitId",
                        column: x => x.JobRequestUnitId,
                        principalTable: "JobRequestUnits",
                        principalColumn: "JobRequestUnitId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MOAForms_JobRequests_JobRequestId",
                        column: x => x.JobRequestId,
                        principalTable: "JobRequests",
                        principalColumn: "JobRequestId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MOAForms_MOIForms_MOIFormId",
                        column: x => x.MOIFormId,
                        principalTable: "MOIForms",
                        principalColumn: "MOIFormId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowInstances",
                columns: table => new
                {
                    WorkflowInstanceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkflowTemplateId = table.Column<int>(type: "integer", nullable: false),
                    FormType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MoiFormId = table.Column<int>(type: "integer", nullable: true),
                    MoaFormId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrentStepOrder = table.Column<int>(type: "integer", nullable: false),
                    ConditionsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowInstances", x => x.WorkflowInstanceId);
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_MOAForms_MoaFormId",
                        column: x => x.MoaFormId,
                        principalTable: "MOAForms",
                        principalColumn: "MOAFormId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_MOIForms_MoiFormId",
                        column: x => x.MoiFormId,
                        principalTable: "MOIForms",
                        principalColumn: "MOIFormId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_WorkflowTemplates_WorkflowTemplateId",
                        column: x => x.WorkflowTemplateId,
                        principalTable: "WorkflowTemplates",
                        principalColumn: "WorkflowTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepInstances",
                columns: table => new
                {
                    WorkflowStepInstanceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkflowInstanceId = table.Column<int>(type: "integer", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    StepKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ConditionType = table.Column<string>(type: "text", nullable: false),
                    AssigneeType = table.Column<string>(type: "text", nullable: false),
                    AssigneeUserId = table.Column<int>(type: "integer", nullable: true),
                    AssigneeName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Comments = table.Column<string>(type: "text", nullable: false),
                    AdminOverridden = table.Column<bool>(type: "boolean", nullable: false),
                    OverriddenByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepInstances", x => x.WorkflowStepInstanceId);
                    table.ForeignKey(
                        name: "FK_WorkflowStepInstances_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "WorkflowInstanceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountHolders_CustomerId",
                table: "AccountHolders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountHolders_UserId",
                table: "AccountHolders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppNotifications_UserId_IsRead",
                table: "AppNotifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_CompletedServices_DateCompleted",
                table: "CompletedServices",
                column: "DateCompleted");

            migrationBuilder.CreateIndex(
                name: "IX_CompletedServices_JobRequestId",
                table: "CompletedServices",
                column: "JobRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPackages_CustomerId",
                table: "CustomerPackages",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DivisionGroupRecommenders_DivisionGroupId",
                table: "DivisionGroupRecommenders",
                column: "DivisionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DivisionGroupRecommenders_UserId",
                table: "DivisionGroupRecommenders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DivisionGroups_Code",
                table: "DivisionGroups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_FormType_Code",
                table: "FormTemplates",
                columns: new[] { "FormType", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId",
                table: "Invoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_JobRequestId",
                table: "Invoices",
                column: "JobRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_JobItemDocuments_JobRequestId",
                table: "JobItemDocuments",
                column: "JobRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_JobItemDocuments_JobRequestUnitId",
                table: "JobItemDocuments",
                column: "JobRequestUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequests_CustomerId",
                table: "JobRequests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequests_CustomerPackageId",
                table: "JobRequests",
                column: "CustomerPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequests_Status",
                table: "JobRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequestUnitAssignees_JobRequestUnitId_UserId",
                table: "JobRequestUnitAssignees",
                columns: new[] { "JobRequestUnitId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobRequestUnitAssignees_UserId",
                table: "JobRequestUnitAssignees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequestUnits_JobRequestId_UnitNumber",
                table: "JobRequestUnits",
                columns: new[] { "JobRequestId", "UnitNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MOAForms_CustomerId",
                table: "MOAForms",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MOAForms_JobRequestId",
                table: "MOAForms",
                column: "JobRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MOAForms_JobRequestUnitId",
                table: "MOAForms",
                column: "JobRequestUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_MOAForms_MOIFormId",
                table: "MOAForms",
                column: "MOIFormId");

            migrationBuilder.CreateIndex(
                name: "IX_MOIForms_CustomerId",
                table: "MOIForms",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MOIForms_JobRequestId",
                table: "MOIForms",
                column: "JobRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MOIForms_JobRequestUnitId",
                table: "MOIForms",
                column: "JobRequestUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageScheduleItems_CustomerId",
                table: "PackageScheduleItems",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageScheduleItems_CustomerPackageId",
                table: "PackageScheduleItems",
                column: "CustomerPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetOtps_Email_CreatedAt",
                table: "PasswordResetOtps",
                columns: new[] { "Email", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobForms_JobRequestId",
                table: "ServiceJobForms",
                column: "JobRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignatoryCustomerAccess_CustomerId",
                table: "SignatoryCustomerAccess",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SignatoryCustomerAccess_UserId_CustomerId",
                table: "SignatoryCustomerAccess",
                columns: new[] { "UserId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CustomerId",
                table: "Users",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_MoaFormId",
                table: "WorkflowInstances",
                column: "MoaFormId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_MoiFormId",
                table: "WorkflowInstances",
                column: "MoiFormId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_WorkflowTemplateId",
                table: "WorkflowInstances",
                column: "WorkflowTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepInstances_WorkflowInstanceId",
                table: "WorkflowStepInstances",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepTemplates_WorkflowTemplateId",
                table: "WorkflowStepTemplates",
                column: "WorkflowTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTemplates_Code",
                table: "WorkflowTemplates",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountHolders");

            migrationBuilder.DropTable(
                name: "AppNotifications");

            migrationBuilder.DropTable(
                name: "BillingParties");

            migrationBuilder.DropTable(
                name: "CompletedServices");

            migrationBuilder.DropTable(
                name: "DivisionGroupRecommenders");

            migrationBuilder.DropTable(
                name: "FormTemplates");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "JobItemDocuments");

            migrationBuilder.DropTable(
                name: "JobRequestUnitAssignees");

            migrationBuilder.DropTable(
                name: "PackageScheduleItems");

            migrationBuilder.DropTable(
                name: "PasswordResetOtps");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "ServiceJobForms");

            migrationBuilder.DropTable(
                name: "SignatoryCustomerAccess");

            migrationBuilder.DropTable(
                name: "WorkflowStepInstances");

            migrationBuilder.DropTable(
                name: "WorkflowStepTemplates");

            migrationBuilder.DropTable(
                name: "DivisionGroups");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WorkflowInstances");

            migrationBuilder.DropTable(
                name: "MOAForms");

            migrationBuilder.DropTable(
                name: "WorkflowTemplates");

            migrationBuilder.DropTable(
                name: "MOIForms");

            migrationBuilder.DropTable(
                name: "JobRequestUnits");

            migrationBuilder.DropTable(
                name: "JobRequests");

            migrationBuilder.DropTable(
                name: "CustomerPackages");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
