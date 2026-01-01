using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessManagement.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DishPlan",
                columns: table => new
                {
                    DishPlanId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayOfWeek = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MealType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DishName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DishPlan", x => x.DishPlanId);
                });

            migrationBuilder.CreateTable(
                name: "MessPeriod",
                columns: table => new
                {
                    PeriodId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FixedWaterCharge = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TeaPricePerCup = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessPeriod", x => x.PeriodId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PasswordResetToken = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PasswordResetTokenExpiry = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "TeaRecord",
                columns: table => new
                {
                    TeaRecordId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalCupsServed = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeaRecord", x => x.TeaRecordId);
                    table.ForeignKey(
                        name: "FK_TeaRecord_MessPeriod_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "MessPeriod",
                        principalColumn: "PeriodId");
                });

            migrationBuilder.CreateTable(
                name: "Attendance",
                columns: table => new
                {
                    AttendanceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BreakfastDishPlanId = table.Column<int>(type: "int", nullable: true),
                    LunchDishPlanId = table.Column<int>(type: "int", nullable: true),
                    DinnerDishPlanId = table.Column<int>(type: "int", nullable: true),
                    DishPlanId = table.Column<int>(type: "int", nullable: true),
                    PeriodId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsBreakfastPresent = table.Column<bool>(type: "bit", nullable: false),
                    IsLunchPresent = table.Column<bool>(type: "bit", nullable: false),
                    IsDinnerPresent = table.Column<bool>(type: "bit", nullable: false),
                    IsPresent = table.Column<bool>(type: "bit", nullable: false),
                    BreakfastVerified = table.Column<bool>(type: "bit", nullable: false),
                    LunchVerified = table.Column<bool>(type: "bit", nullable: false),
                    DinnerVerified = table.Column<bool>(type: "bit", nullable: false),
                    BreakfastVerifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LunchVerifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DinnerVerifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedByUser = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendance", x => x.AttendanceId);
                    table.ForeignKey(
                        name: "FK_Attendance_DishPlan_BreakfastDishPlanId",
                        column: x => x.BreakfastDishPlanId,
                        principalTable: "DishPlan",
                        principalColumn: "DishPlanId");
                    table.ForeignKey(
                        name: "FK_Attendance_DishPlan_DinnerDishPlanId",
                        column: x => x.DinnerDishPlanId,
                        principalTable: "DishPlan",
                        principalColumn: "DishPlanId");
                    table.ForeignKey(
                        name: "FK_Attendance_DishPlan_DishPlanId",
                        column: x => x.DishPlanId,
                        principalTable: "DishPlan",
                        principalColumn: "DishPlanId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Attendance_DishPlan_LunchDishPlanId",
                        column: x => x.LunchDishPlanId,
                        principalTable: "DishPlan",
                        principalColumn: "DishPlanId");
                    table.ForeignKey(
                        name: "FK_Attendance_MessPeriod_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "MessPeriod",
                        principalColumn: "PeriodId");
                    table.ForeignKey(
                        name: "FK_Attendance_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TeaEntry",
                columns: table => new
                {
                    TeaEntryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PeriodId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Cups = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    VerifiedByUser = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeaEntry", x => x.TeaEntryId);
                    table.ForeignKey(
                        name: "FK_TeaEntry_MessPeriod_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "MessPeriod",
                        principalColumn: "PeriodId");
                    table.ForeignKey(
                        name: "FK_TeaEntry_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Payment",
                columns: table => new
                {
                    PaymentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PeriodId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StripePaymentId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AttendanceId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payment", x => x.PaymentId);
                    table.ForeignKey(
                        name: "FK_Payment_Attendance_AttendanceId",
                        column: x => x.AttendanceId,
                        principalTable: "Attendance",
                        principalColumn: "AttendanceId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Payment_MessPeriod_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "MessPeriod",
                        principalColumn: "PeriodId");
                    table.ForeignKey(
                        name: "FK_Payment_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_Payment_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_BreakfastDishPlanId",
                table: "Attendance",
                column: "BreakfastDishPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_DinnerDishPlanId",
                table: "Attendance",
                column: "DinnerDishPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_DishPlanId",
                table: "Attendance",
                column: "DishPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_LunchDishPlanId",
                table: "Attendance",
                column: "LunchDishPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_PeriodId",
                table: "Attendance",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_UserId",
                table: "Attendance",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_ApprovedByUserId",
                table: "Payment",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_AttendanceId",
                table: "Payment",
                column: "AttendanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_PeriodId",
                table: "Payment",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_UserId",
                table: "Payment",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeaEntry_PeriodId",
                table: "TeaEntry",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_TeaEntry_UserId_Date",
                table: "TeaEntry",
                columns: new[] { "UserId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeaRecord_PeriodId",
                table: "TeaRecord",
                column: "PeriodId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payment");

            migrationBuilder.DropTable(
                name: "TeaEntry");

            migrationBuilder.DropTable(
                name: "TeaRecord");

            migrationBuilder.DropTable(
                name: "Attendance");

            migrationBuilder.DropTable(
                name: "DishPlan");

            migrationBuilder.DropTable(
                name: "MessPeriod");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
