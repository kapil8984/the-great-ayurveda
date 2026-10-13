using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ayurveda.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixPasswordResetOtpsAndOrderItemReviewStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- 1. Align PasswordResetOtps columns
                IF COL_LENGTH('PasswordResetOtps', 'AttemptCount') IS NOT NULL AND COL_LENGTH('PasswordResetOtps', 'Attempts') IS NULL
                BEGIN
                    EXEC sp_rename 'PasswordResetOtps.AttemptCount', 'Attempts', 'COLUMN';
                END
                ELSE IF COL_LENGTH('PasswordResetOtps', 'Attempts') IS NULL
                BEGIN
                    ALTER TABLE [PasswordResetOtps] ADD [Attempts] INT NOT NULL CONSTRAINT [DF_PasswordResetOtps_Attempts] DEFAULT 0;
                END

                IF COL_LENGTH('PasswordResetOtps', 'ExpiryTime') IS NOT NULL AND COL_LENGTH('PasswordResetOtps', 'ExpiryUtc') IS NULL
                BEGIN
                    EXEC sp_rename 'PasswordResetOtps.ExpiryTime', 'ExpiryUtc', 'COLUMN';
                END
                ELSE IF COL_LENGTH('PasswordResetOtps', 'ExpiryUtc') IS NULL
                BEGIN
                    ALTER TABLE [PasswordResetOtps] ADD [ExpiryUtc] DATETIME2 NOT NULL CONSTRAINT [DF_PasswordResetOtps_ExpiryUtc] DEFAULT GETUTCDATE();
                END

                -- 2. Align OrderItems ReviewStatus column and default constraint
                IF COL_LENGTH('OrderItems', 'ReviewStatus') IS NULL
                BEGIN
                    ALTER TABLE [OrderItems] ADD [ReviewStatus] INT NOT NULL CONSTRAINT [DF_OrderItems_ReviewStatus] DEFAULT 0;
                END
                ELSE
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM sys.default_constraints 
                        WHERE parent_object_id = OBJECT_ID('OrderItems') 
                        AND col_name(parent_object_id, parent_column_id) = 'ReviewStatus'
                    )
                    BEGIN
                        ALTER TABLE [OrderItems] ADD CONSTRAINT [DF_OrderItems_ReviewStatus] DEFAULT 0 FOR [ReviewStatus];
                    END
                END

                -- 3. Cleanup any leftover shadow FK columns or constraints if any exist
                IF OBJECT_ID('FK_Appointments_Users_UserId1', 'F') IS NOT NULL ALTER TABLE [Appointments] DROP CONSTRAINT [FK_Appointments_Users_UserId1];
                IF OBJECT_ID('FK_Carts_Users_UserId1', 'F') IS NOT NULL ALTER TABLE [Carts] DROP CONSTRAINT [FK_Carts_Users_UserId1];
                IF OBJECT_ID('FK_Orders_Users_UserId1', 'F') IS NOT NULL ALTER TABLE [Orders] DROP CONSTRAINT [FK_Orders_Users_UserId1];
                IF OBJECT_ID('FK_ProductReviews_Users_UserId1', 'F') IS NOT NULL ALTER TABLE [ProductReviews] DROP CONSTRAINT [FK_ProductReviews_Users_UserId1];
                IF OBJECT_ID('FK_YogaReviews_Users_UserId1', 'F') IS NOT NULL ALTER TABLE [YogaReviews] DROP CONSTRAINT [FK_YogaReviews_Users_UserId1];

                IF INDEXPROPERTY(OBJECT_ID('YogaReviews'), 'IX_YogaReviews_UserId1', 'IndexId') IS NOT NULL DROP INDEX [IX_YogaReviews_UserId1] ON [YogaReviews];
                IF INDEXPROPERTY(OBJECT_ID('ProductReviews'), 'IX_ProductReviews_UserId1', 'IndexId') IS NOT NULL DROP INDEX [IX_ProductReviews_UserId1] ON [ProductReviews];
                IF INDEXPROPERTY(OBJECT_ID('Orders'), 'IX_Orders_UserId1', 'IndexId') IS NOT NULL DROP INDEX [IX_Orders_UserId1] ON [Orders];
                IF INDEXPROPERTY(OBJECT_ID('Carts'), 'IX_Carts_UserId1', 'IndexId') IS NOT NULL DROP INDEX [IX_Carts_UserId1] ON [Carts];
                IF INDEXPROPERTY(OBJECT_ID('Appointments'), 'IX_Appointments_UserId1', 'IndexId') IS NOT NULL DROP INDEX [IX_Appointments_UserId1] ON [Appointments];

                IF COL_LENGTH('YogaReviews', 'UserId1') IS NOT NULL ALTER TABLE [YogaReviews] DROP COLUMN [UserId1];
                IF COL_LENGTH('ProductReviews', 'UserId1') IS NOT NULL ALTER TABLE [ProductReviews] DROP COLUMN [UserId1];
                IF COL_LENGTH('Orders', 'UserId1') IS NOT NULL ALTER TABLE [Orders] DROP COLUMN [UserId1];
                IF COL_LENGTH('Carts', 'UserId1') IS NOT NULL ALTER TABLE [Carts] DROP COLUMN [UserId1];
                IF COL_LENGTH('Appointments', 'UserId1') IS NOT NULL ALTER TABLE [Appointments] DROP COLUMN [UserId1];
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- Revert OrderItems default constraint if needed
                IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('OrderItems') AND name = 'DF_OrderItems_ReviewStatus')
                BEGIN
                    ALTER TABLE [OrderItems] DROP CONSTRAINT [DF_OrderItems_ReviewStatus];
                END
            ");
        }
    }
}
