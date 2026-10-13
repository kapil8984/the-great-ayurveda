using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ayurveda.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveShadowFksAndAlignModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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
                IF INDEXPROPERTY(OBJECT_ID('Appointments'), 'IX_Appointments_DoctorId_StartUtc', 'IndexId') IS NOT NULL DROP INDEX [IX_Appointments_DoctorId_StartUtc] ON [Appointments];

                IF COL_LENGTH('YogaReviews', 'UserId1') IS NOT NULL ALTER TABLE [YogaReviews] DROP COLUMN [UserId1];
                IF COL_LENGTH('ProductReviews', 'UserId1') IS NOT NULL ALTER TABLE [ProductReviews] DROP COLUMN [UserId1];
                IF COL_LENGTH('Orders', 'UserId1') IS NOT NULL ALTER TABLE [Orders] DROP COLUMN [UserId1];
                IF COL_LENGTH('Carts', 'UserId1') IS NOT NULL ALTER TABLE [Carts] DROP COLUMN [UserId1];
                IF COL_LENGTH('Appointments', 'UserId1') IS NOT NULL ALTER TABLE [Appointments] DROP COLUMN [UserId1];

                IF COL_LENGTH('Appointments', 'CallDurationSeconds') IS NULL ALTER TABLE [Appointments] ADD [CallDurationSeconds] int NOT NULL DEFAULT 0;
            ");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('Orders', 'CancellationReason') IS NULL ALTER TABLE [Orders] ADD [CancellationReason] nvarchar(max) NULL;
                IF COL_LENGTH('Orders', 'DeliveryType') IS NULL ALTER TABLE [Orders] ADD [DeliveryType] nvarchar(max) NOT NULL DEFAULT '';
                IF COL_LENGTH('Orders', 'IdempotencyKey') IS NULL ALTER TABLE [Orders] ADD [IdempotencyKey] nvarchar(max) NULL;
                IF COL_LENGTH('Orders', 'ReturnReason') IS NULL ALTER TABLE [Orders] ADD [ReturnReason] nvarchar(max) NULL;
                IF COL_LENGTH('Orders', 'ReturnStatus') IS NULL ALTER TABLE [Orders] ADD [ReturnStatus] nvarchar(max) NULL;

                IF COL_LENGTH('DoctorDetails', 'IsDemoData') IS NULL ALTER TABLE [DoctorDetails] ADD [IsDemoData] bit NOT NULL DEFAULT 0;

                IF COL_LENGTH('Appointments', 'DietaryAdvice') IS NULL ALTER TABLE [Appointments] ADD [DietaryAdvice] nvarchar(max) NULL;
                IF COL_LENGTH('Appointments', 'DoshaDiagnosis') IS NULL ALTER TABLE [Appointments] ADD [DoshaDiagnosis] nvarchar(max) NULL;
                IF COL_LENGTH('Appointments', 'FollowUpDateUtc') IS NULL ALTER TABLE [Appointments] ADD [FollowUpDateUtc] datetime2 NULL;
                IF COL_LENGTH('Appointments', 'JoinedAtUtc') IS NULL ALTER TABLE [Appointments] ADD [JoinedAtUtc] datetime2 NULL;
                IF COL_LENGTH('Appointments', 'LeftAtUtc') IS NULL ALTER TABLE [Appointments] ADD [LeftAtUtc] datetime2 NULL;
                IF COL_LENGTH('Appointments', 'PrescriptionProductsJson') IS NULL ALTER TABLE [Appointments] ADD [PrescriptionProductsJson] nvarchar(max) NULL;

                IF OBJECT_ID(N'[DoctorReviews]') IS NULL
                CREATE TABLE [DoctorReviews] (
                    [Id] int IDENTITY(1,1) NOT NULL,
                    [DoctorId] int NOT NULL,
                    [UserId] int NOT NULL,
                    [AppointmentId] int NOT NULL,
                    [Rating] int NOT NULL,
                    [ReviewText] nvarchar(max) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_DoctorReviews] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_DoctorReviews_Appointments_AppointmentId] FOREIGN KEY ([AppointmentId]) REFERENCES [Appointments] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_DoctorReviews_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_DoctorReviews_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [Doctors] ([Id]) ON DELETE NO ACTION
                );

                IF OBJECT_ID(N'[ProductQuestions]') IS NULL
                CREATE TABLE [ProductQuestions] (
                    [Id] int IDENTITY(1,1) NOT NULL,
                    [ProductId] int NOT NULL,
                    [QuestionText] nvarchar(500) NOT NULL,
                    [AnswerText] nvarchar(max) NOT NULL,
                    [AskedBy] nvarchar(max) NULL,
                    [AnsweredBy] nvarchar(max) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_ProductQuestions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ProductQuestions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
                );

                IF INDEXPROPERTY(OBJECT_ID('Appointments'), 'IX_Appointments_DoctorId_StartUtc', 'IndexId') IS NULL
                CREATE UNIQUE INDEX [IX_Appointments_DoctorId_StartUtc] ON [Appointments] ([DoctorId], [StartUtc]) WHERE [Status] <> 'Cancelled' AND [Status] <> 'Declined';

                IF INDEXPROPERTY(OBJECT_ID('DoctorReviews'), 'IX_DoctorReviews_AppointmentId', 'IndexId') IS NULL
                CREATE UNIQUE INDEX [IX_DoctorReviews_AppointmentId] ON [DoctorReviews] ([AppointmentId]);

                IF INDEXPROPERTY(OBJECT_ID('DoctorReviews'), 'IX_DoctorReviews_DoctorId', 'IndexId') IS NULL
                CREATE INDEX [IX_DoctorReviews_DoctorId] ON [DoctorReviews] ([DoctorId]);

                IF INDEXPROPERTY(OBJECT_ID('DoctorReviews'), 'IX_DoctorReviews_UserId', 'IndexId') IS NULL
                CREATE INDEX [IX_DoctorReviews_UserId] ON [DoctorReviews] ([UserId]);

                IF INDEXPROPERTY(OBJECT_ID('ProductQuestions'), 'IX_ProductQuestions_ProductId', 'IndexId') IS NULL
                CREATE INDEX [IX_ProductQuestions_ProductId] ON [ProductQuestions] ([ProductId]);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DoctorReviews");

            migrationBuilder.DropTable(
                name: "ProductQuestions");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_StartUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryType",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReturnReason",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReturnStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsDemoData",
                table: "DoctorDetails");

            migrationBuilder.DropColumn(
                name: "DietaryAdvice",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DoshaDiagnosis",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "FollowUpDateUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "JoinedAtUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "LeftAtUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PrescriptionProductsJson",
                table: "Appointments");

            migrationBuilder.RenameColumn(
                name: "CallDurationSeconds",
                table: "Appointments",
                newName: "UserId1");

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "YogaReviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "ProductReviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "Carts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_YogaReviews_UserId1",
                table: "YogaReviews",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_UserId1",
                table: "ProductReviews",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId1",
                table: "Orders",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_UserId1",
                table: "Carts",
                column: "UserId1",
                unique: true,
                filter: "[UserId1] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_StartUtc",
                table: "Appointments",
                columns: new[] { "DoctorId", "StartUtc" },
                unique: true,
                filter: "[Status] <> 'Cancelled' AND [Status] <> 'Declined'");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_UserId1",
                table: "Appointments",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Users_UserId1",
                table: "Appointments",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Carts_Users_UserId1",
                table: "Carts",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Users_UserId1",
                table: "Orders",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductReviews_Users_UserId1",
                table: "ProductReviews",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_YogaReviews_Users_UserId1",
                table: "YogaReviews",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
