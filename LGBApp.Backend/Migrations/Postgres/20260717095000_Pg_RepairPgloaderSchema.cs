using LGBApp.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGBApp.Backend.Migrations.Postgres;

/// <summary>
/// Repairs the schema drift left by the SQLite → Postgres pgloader import
/// (docs/deploy/migrate.load.example runs "with data only, drop indexes"): the load
/// dropped every primary key, foreign key and index, and its post-load recreate never
/// completed, so <c>Pg_Baseline</c> was stamped against a keyless database.
///
/// Two consequences this undoes:
///   1. <c>Pg_EmailActionTokens</c> could not create its foreign key to
///      <c>WorkflowStepInstances</c>, which has blocked every deploy since 17 Jul 2026.
///   2. Identity sequences were left at 1, so EF inserts silently reused live ids —
///      <c>WorkflowStepTemplates</c> already holds three colliding rows.
///
/// Every statement is guarded, so this is a no-op on a database that was built
/// correctly from <c>Pg_Baseline</c>.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260717095000_Pg_RepairPgloaderSchema")]
public partial class Pg_RepairPgloaderSchema : Migration
{
    /// <summary>Tables the load stripped, with the column their primary key belongs on.</summary>
    private static readonly (string Table, string Column)[] PrimaryKeys =
    [
        ("AccountHolders", "AccountHolderId"),
        ("AppNotifications", "AppNotificationId"),
        ("BillingParties", "BillingPartyId"),
        ("CompletedServices", "Id"),
        ("CustomerPackages", "CustomerPackageId"),
        ("Customers", "CustomerId"),
        ("DivisionGroupRecommenders", "DivisionGroupRecommenderId"),
        ("DivisionGroups", "DivisionGroupId"),
        ("FormTemplates", "FormTemplateId"),
        ("Invoices", "InvoiceId"),
        ("JobItemDocuments", "JobItemDocumentId"),
        ("JobRequestUnitAssignees", "JobRequestUnitAssigneeId"),
        ("JobRequestUnits", "JobRequestUnitId"),
        ("JobRequests", "JobRequestId"),
        ("MOAForms", "MOAFormId"),
        ("MOIForms", "MOIFormId"),
        ("PackageScheduleItems", "PackageScheduleItemId"),
        ("PasswordResetOtps", "PasswordResetOtpId"),
        ("Products", "ProductId"),
        ("ServiceJobForms", "ServiceJobFormId"),
        ("SignatoryCustomerAccess", "SignatoryCustomerAccessId"),
        ("Users", "UserId"),
        ("WorkflowInstances", "WorkflowInstanceId"),
        ("WorkflowStepInstances", "WorkflowStepInstanceId"),
        ("WorkflowStepTemplates", "WorkflowStepTemplateId"),
        ("WorkflowTemplates", "WorkflowTemplateId"),
    ];

    /// <summary>Copied from the Pg_Baseline CREATE TABLE bodies, which the load discarded.</summary>
    private static readonly (string Name, string Table, string Column, string Principal, string PrincipalColumn, string OnDelete)[] ForeignKeys =
    [
        ("FK_AccountHolders_Customers_CustomerId", "AccountHolders", "CustomerId", "Customers", "CustomerId", "CASCADE"),
        ("FK_AccountHolders_Users_UserId", "AccountHolders", "UserId", "Users", "UserId", "SET NULL"),
        ("FK_CustomerPackages_Customers_CustomerId", "CustomerPackages", "CustomerId", "Customers", "CustomerId", "CASCADE"),
        ("FK_DivisionGroupRecommenders_DivisionGroups_DivisionGroupId", "DivisionGroupRecommenders", "DivisionGroupId", "DivisionGroups", "DivisionGroupId", "CASCADE"),
        ("FK_DivisionGroupRecommenders_Users_UserId", "DivisionGroupRecommenders", "UserId", "Users", "UserId", "SET NULL"),
        ("FK_Invoices_Customers_CustomerId", "Invoices", "CustomerId", "Customers", "CustomerId", "RESTRICT"),
        ("FK_Invoices_JobRequests_JobRequestId", "Invoices", "JobRequestId", "JobRequests", "JobRequestId", "SET NULL"),
        ("FK_JobItemDocuments_JobRequestUnits_JobRequestUnitId", "JobItemDocuments", "JobRequestUnitId", "JobRequestUnits", "JobRequestUnitId", "NO ACTION"),
        ("FK_JobItemDocuments_JobRequests_JobRequestId", "JobItemDocuments", "JobRequestId", "JobRequests", "JobRequestId", "CASCADE"),
        ("FK_JobRequestUnitAssignees_JobRequestUnits_JobRequestUnitId", "JobRequestUnitAssignees", "JobRequestUnitId", "JobRequestUnits", "JobRequestUnitId", "CASCADE"),
        ("FK_JobRequestUnitAssignees_Users_UserId", "JobRequestUnitAssignees", "UserId", "Users", "UserId", "CASCADE"),
        ("FK_JobRequestUnits_JobRequests_JobRequestId", "JobRequestUnits", "JobRequestId", "JobRequests", "JobRequestId", "CASCADE"),
        ("FK_JobRequests_CustomerPackages_CustomerPackageId", "JobRequests", "CustomerPackageId", "CustomerPackages", "CustomerPackageId", "SET NULL"),
        ("FK_JobRequests_Customers_CustomerId", "JobRequests", "CustomerId", "Customers", "CustomerId", "SET NULL"),
        ("FK_MOAForms_Customers_CustomerId", "MOAForms", "CustomerId", "Customers", "CustomerId", "SET NULL"),
        ("FK_MOAForms_JobRequestUnits_JobRequestUnitId", "MOAForms", "JobRequestUnitId", "JobRequestUnits", "JobRequestUnitId", "SET NULL"),
        ("FK_MOAForms_JobRequests_JobRequestId", "MOAForms", "JobRequestId", "JobRequests", "JobRequestId", "SET NULL"),
        ("FK_MOAForms_MOIForms_MOIFormId", "MOAForms", "MOIFormId", "MOIForms", "MOIFormId", "SET NULL"),
        ("FK_MOIForms_Customers_CustomerId", "MOIForms", "CustomerId", "Customers", "CustomerId", "SET NULL"),
        ("FK_MOIForms_JobRequestUnits_JobRequestUnitId", "MOIForms", "JobRequestUnitId", "JobRequestUnits", "JobRequestUnitId", "SET NULL"),
        ("FK_MOIForms_JobRequests_JobRequestId", "MOIForms", "JobRequestId", "JobRequests", "JobRequestId", "SET NULL"),
        ("FK_PackageScheduleItems_CustomerPackages_CustomerPackageId", "PackageScheduleItems", "CustomerPackageId", "CustomerPackages", "CustomerPackageId", "CASCADE"),
        ("FK_PackageScheduleItems_Customers_CustomerId", "PackageScheduleItems", "CustomerId", "Customers", "CustomerId", "CASCADE"),
        ("FK_ServiceJobForms_JobRequests_JobRequestId", "ServiceJobForms", "JobRequestId", "JobRequests", "JobRequestId", "CASCADE"),
        ("FK_SignatoryCustomerAccess_Customers_CustomerId", "SignatoryCustomerAccess", "CustomerId", "Customers", "CustomerId", "CASCADE"),
        ("FK_SignatoryCustomerAccess_Users_UserId", "SignatoryCustomerAccess", "UserId", "Users", "UserId", "CASCADE"),
        ("FK_Users_Customers_CustomerId", "Users", "CustomerId", "Customers", "CustomerId", "SET NULL"),
        ("FK_WorkflowInstances_MOAForms_MoaFormId", "WorkflowInstances", "MoaFormId", "MOAForms", "MOAFormId", "CASCADE"),
        ("FK_WorkflowInstances_MOIForms_MoiFormId", "WorkflowInstances", "MoiFormId", "MOIForms", "MOIFormId", "CASCADE"),
        ("FK_WorkflowInstances_WorkflowTemplates_WorkflowTemplateId", "WorkflowInstances", "WorkflowTemplateId", "WorkflowTemplates", "WorkflowTemplateId", "CASCADE"),
        ("FK_WorkflowStepInstances_WorkflowInstances_WorkflowInstanceId", "WorkflowStepInstances", "WorkflowInstanceId", "WorkflowInstances", "WorkflowInstanceId", "CASCADE"),
        ("FK_WorkflowStepTemplates_WorkflowTemplates_WorkflowTemplateId", "WorkflowStepTemplates", "WorkflowTemplateId", "WorkflowTemplates", "WorkflowTemplateId", "CASCADE"),
    ];

    /// <summary>Also from Pg_Baseline. Tables created by later migrations kept their indexes.</summary>
    private static readonly (string Name, string Table, string Columns, bool Unique)[] Indexes =
    [
        ("IX_AccountHolders_CustomerId", "AccountHolders", "\"CustomerId\"", false),
        ("IX_AccountHolders_UserId", "AccountHolders", "\"UserId\"", false),
        ("IX_AppNotifications_UserId_IsRead", "AppNotifications", "\"UserId\", \"IsRead\"", false),
        ("IX_CompletedServices_DateCompleted", "CompletedServices", "\"DateCompleted\"", false),
        ("IX_CompletedServices_JobRequestId", "CompletedServices", "\"JobRequestId\"", false),
        ("IX_CustomerPackages_CustomerId", "CustomerPackages", "\"CustomerId\"", false),
        ("IX_DivisionGroupRecommenders_DivisionGroupId", "DivisionGroupRecommenders", "\"DivisionGroupId\"", false),
        ("IX_DivisionGroupRecommenders_UserId", "DivisionGroupRecommenders", "\"UserId\"", false),
        ("IX_DivisionGroups_Code", "DivisionGroups", "\"Code\"", true),
        ("IX_FormTemplates_FormType_Code", "FormTemplates", "\"FormType\", \"Code\"", true),
        ("IX_Invoices_CustomerId", "Invoices", "\"CustomerId\"", false),
        ("IX_Invoices_InvoiceNumber", "Invoices", "\"InvoiceNumber\"", true),
        ("IX_Invoices_JobRequestId", "Invoices", "\"JobRequestId\"", false),
        ("IX_JobItemDocuments_JobRequestId", "JobItemDocuments", "\"JobRequestId\"", false),
        ("IX_JobItemDocuments_JobRequestUnitId", "JobItemDocuments", "\"JobRequestUnitId\"", false),
        ("IX_JobRequests_CustomerId", "JobRequests", "\"CustomerId\"", false),
        ("IX_JobRequests_CustomerPackageId", "JobRequests", "\"CustomerPackageId\"", false),
        ("IX_JobRequests_Status", "JobRequests", "\"Status\"", false),
        ("IX_JobRequestUnitAssignees_JobRequestUnitId_UserId", "JobRequestUnitAssignees", "\"JobRequestUnitId\", \"UserId\"", true),
        ("IX_JobRequestUnitAssignees_UserId", "JobRequestUnitAssignees", "\"UserId\"", false),
        ("IX_JobRequestUnits_JobRequestId_UnitNumber", "JobRequestUnits", "\"JobRequestId\", \"UnitNumber\"", true),
        ("IX_MOAForms_CustomerId", "MOAForms", "\"CustomerId\"", false),
        ("IX_MOAForms_JobRequestId", "MOAForms", "\"JobRequestId\"", false),
        ("IX_MOAForms_JobRequestUnitId", "MOAForms", "\"JobRequestUnitId\"", false),
        ("IX_MOAForms_MOIFormId", "MOAForms", "\"MOIFormId\"", false),
        ("IX_MOIForms_CustomerId", "MOIForms", "\"CustomerId\"", false),
        ("IX_MOIForms_JobRequestId", "MOIForms", "\"JobRequestId\"", false),
        ("IX_MOIForms_JobRequestUnitId", "MOIForms", "\"JobRequestUnitId\"", false),
        ("IX_PackageScheduleItems_CustomerId", "PackageScheduleItems", "\"CustomerId\"", false),
        ("IX_PackageScheduleItems_CustomerPackageId", "PackageScheduleItems", "\"CustomerPackageId\"", false),
        ("IX_PasswordResetOtps_Email_CreatedAt", "PasswordResetOtps", "\"Email\", \"CreatedAt\"", false),
        ("IX_ServiceJobForms_JobRequestId", "ServiceJobForms", "\"JobRequestId\"", true),
        ("IX_SignatoryCustomerAccess_CustomerId", "SignatoryCustomerAccess", "\"CustomerId\"", false),
        ("IX_SignatoryCustomerAccess_UserId_CustomerId", "SignatoryCustomerAccess", "\"UserId\", \"CustomerId\"", true),
        ("IX_Users_CustomerId", "Users", "\"CustomerId\"", false),
        ("IX_Users_Email", "Users", "\"Email\"", true),
        ("IX_WorkflowInstances_MoaFormId", "WorkflowInstances", "\"MoaFormId\"", false),
        ("IX_WorkflowInstances_MoiFormId", "WorkflowInstances", "\"MoiFormId\"", false),
        ("IX_WorkflowInstances_WorkflowTemplateId", "WorkflowInstances", "\"WorkflowTemplateId\"", false),
        ("IX_WorkflowStepInstances_WorkflowInstanceId", "WorkflowStepInstances", "\"WorkflowInstanceId\"", false),
        ("IX_WorkflowStepTemplates_WorkflowTemplateId", "WorkflowStepTemplates", "\"WorkflowTemplateId\"", false),
        ("IX_WorkflowTemplates_Code", "WorkflowTemplates", "\"Code\"", true),
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        DeduplicateWorkflowStepTemplateIds(migrationBuilder);
        AddMissingPrimaryKeys(migrationBuilder);
        AddMissingIndexes(migrationBuilder);
        AddMissingForeignKeys(migrationBuilder);
        RealignIdentitySequences(migrationBuilder);
    }

    /// <summary>
    /// The unset sequence handed EF ids 1-3 a second time when W5 reseeded the MOA chains,
    /// so the same id now names both an MOI step and an MOA step. Keep the earliest template's
    /// row on the contested id and move the later one to the top of the range; nothing
    /// references these ids, since step instances copy the fields rather than pointing at them.
    /// </summary>
    private static void DeduplicateWorkflowStepTemplateIds(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            WITH ranked AS (
                SELECT ctid,
                       row_number() OVER (
                           PARTITION BY "WorkflowStepTemplateId"
                           ORDER BY "WorkflowTemplateId", "StepOrder") AS copy_number
                FROM "WorkflowStepTemplates"
            ),
            renumbered AS (
                SELECT ctid, row_number() OVER (ORDER BY ctid) AS offset_within_dupes
                FROM ranked
                WHERE copy_number > 1
            )
            UPDATE "WorkflowStepTemplates" AS target
            SET "WorkflowStepTemplateId" =
                (SELECT COALESCE(MAX("WorkflowStepTemplateId"), 0) FROM "WorkflowStepTemplates")
                + renumbered.offset_within_dupes
            FROM renumbered
            WHERE target.ctid = renumbered.ctid;
            """);

    /// <summary>
    /// AppNotifications kept a bare unique index named PK_AppNotifications, so promote an
    /// existing index where there is one rather than colliding with its name.
    /// </summary>
    private static void AddMissingPrimaryKeys(MigrationBuilder migrationBuilder)
    {
        var rows = string.Join(",\n            ", PrimaryKeys.Select(pk => $"('{pk.Table}', '{pk.Column}')"));

        migrationBuilder.Sql($"""
            DO $$
            DECLARE
                target record;
                constraint_name text;
                index_present boolean;
            BEGIN
                FOR target IN
                    SELECT * FROM (VALUES
                        {rows}
                    ) AS t(table_name, key_column)
                LOOP
                    CONTINUE WHEN EXISTS (
                        SELECT 1
                        FROM pg_constraint c
                        JOIN pg_class rel ON rel.oid = c.conrelid
                        JOIN pg_namespace ns ON ns.oid = rel.relnamespace
                        WHERE ns.nspname = 'public'
                          AND rel.relname = target.table_name
                          AND c.contype = 'p');

                    constraint_name := 'PK_' || target.table_name;

                    SELECT EXISTS (
                        SELECT 1
                        FROM pg_class idx
                        JOIN pg_namespace ns ON ns.oid = idx.relnamespace
                        WHERE ns.nspname = 'public'
                          AND idx.relname = constraint_name
                          AND idx.relkind = 'i')
                    INTO index_present;

                    IF index_present THEN
                        EXECUTE format(
                            'ALTER TABLE %I ADD CONSTRAINT %I PRIMARY KEY USING INDEX %I',
                            target.table_name, constraint_name, constraint_name);
                    ELSE
                        EXECUTE format(
                            'ALTER TABLE %I ADD CONSTRAINT %I PRIMARY KEY (%I)',
                            target.table_name, constraint_name, target.key_column);
                    END IF;
                END LOOP;
            END $$;
            """);
    }

    private static void AddMissingIndexes(MigrationBuilder migrationBuilder)
    {
        foreach (var (name, table, columns, unique) in Indexes)
        {
            migrationBuilder.Sql(
                $"""CREATE {(unique ? "UNIQUE " : "")}INDEX IF NOT EXISTS "{name}" ON "{table}" ({columns});""");
        }
    }

    private static void AddMissingForeignKeys(MigrationBuilder migrationBuilder)
    {
        foreach (var (name, table, column, principal, principalColumn, onDelete) in ForeignKeys)
        {
            migrationBuilder.Sql($"""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = '{name}'
                    ) THEN
                        ALTER TABLE "{table}"
                            ADD CONSTRAINT "{name}" FOREIGN KEY ("{column}")
                            REFERENCES "{principal}" ("{principalColumn}") ON DELETE {onDelete};
                    END IF;
                END $$;
                """);
        }
    }

    /// <summary>
    /// pgloader was asked to reset sequences and did not, so every identity still starts at 1
    /// and hands out ids that live rows already hold. Only ever moves a sequence forward.
    /// </summary>
    private static void RealignIdentitySequences(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("""
            DO $$
            DECLARE
                identity_column record;
                highest_id bigint;
                sequence_position bigint;
            BEGIN
                FOR identity_column IN
                    SELECT rel.relname AS table_name,
                           att.attname AS column_name,
                           pg_get_serial_sequence(
                               quote_ident(ns.nspname) || '.' || quote_ident(rel.relname),
                               att.attname) AS sequence_name
                    FROM pg_class rel
                    JOIN pg_namespace ns ON ns.oid = rel.relnamespace
                    JOIN pg_attribute att ON att.attrelid = rel.oid
                    WHERE ns.nspname = 'public'
                      AND rel.relkind = 'r'
                      AND att.attidentity <> ''
                LOOP
                    CONTINUE WHEN identity_column.sequence_name IS NULL;

                    EXECUTE format('SELECT COALESCE(MAX(%I), 0) FROM %I',
                                   identity_column.column_name, identity_column.table_name)
                    INTO highest_id;

                    EXECUTE format('SELECT COALESCE(last_value, 0) FROM %s',
                                   identity_column.sequence_name)
                    INTO sequence_position;

                    IF highest_id > sequence_position THEN
                        PERFORM setval(identity_column.sequence_name, highest_id, true);
                    END IF;
                END LOOP;
            END $$;
            """);

    /// <summary>
    /// Deliberately empty. Every statement above is a guarded repair, so it cannot tell
    /// the keys it created apart from the ones Pg_Baseline was meant to create — dropping
    /// them on rollback would corrupt a healthy schema rather than restore one.
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
