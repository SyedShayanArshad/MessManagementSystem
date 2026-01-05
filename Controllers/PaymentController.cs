using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MessManagement.Data;
using MessManagement.Models;
using MessManagement.ViewModels;
using MessManagement.Services;
using Stripe;
using Stripe.Checkout;

namespace MessManagement.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(ApplicationDbContext context, IEmailService emailService, ILogger<PaymentController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        // Admin dashboard showing payment summaries
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(int? periodId)
        {
            var periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
            
            // Get current/active period or most recent
            MessPeriod? currentPeriod = null;
            if (periodId.HasValue)
            {
                currentPeriod = periods.FirstOrDefault(p => p.PeriodId == periodId);
            }
            currentPeriod ??= periods.FirstOrDefault(p => p.IsActive) ?? periods.FirstOrDefault();

            var viewModel = new PaymentSummaryViewModel
            {
                CurrentPeriod = currentPeriod,
                AllPeriods = periods
            };

            if (currentPeriod == null)
            {
                return View(viewModel);
            }

            var start = currentPeriod.StartDate;
            var end = currentPeriod.EndDate;

            // Get all active users
            var users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            
            // Get all dish plans for charge calculation
            var dishPlans = await _context.DishPlans.ToListAsync();
            
            // Get all attendances for this period
            var allAttendances = await _context.Attendances
                .Where(a => a.Date >= start && a.Date <= end)
                .Include(a => a.BreakfastDishPlan)
                .Include(a => a.LunchDishPlan)
                .Include(a => a.DinnerDishPlan)
                .ToListAsync();
            
            // Get all tea records for this period (total cups distributed among users who had attendance)
            var teaRecords = await _context.TeaRecords
                .Where(t => t.Date >= start && t.Date <= end)
                .ToListAsync();
            var totalTeaCups = teaRecords.Sum(t => t.TotalCupsServed);

            // Get all tea entries (per-user actual consumption)
            var allTeaEntries = await _context.TeaEntries
                .Where(t => t.PeriodId == currentPeriod.PeriodId || (t.Date >= start && t.Date <= end))
                .ToListAsync();
            
            // Get all APPROVED payments for this period (only count approved/completed)
            var allPayments = await _context.Payments
                .Where(p => p.PeriodId == currentPeriod.PeriodId && 
                       (p.Status == PaymentStatus.Approved || p.Status == PaymentStatus.Completed))
                .Include(p => p.User)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            // Get pending payments count for alert (exclude "Auto" payments - attendance charges)
            var pendingPaymentsCount = await _context.Payments
                .CountAsync(p => p.PeriodId == currentPeriod.PeriodId && p.Status == PaymentStatus.Pending && p.PaymentMethod != "Auto");
            ViewBag.PendingPaymentsCount = pendingPaymentsCount;

            foreach (var user in users)
            {
                var userAttendances = allAttendances.Where(a => a.UserId == user.UserId).ToList();
                var userPayments = allPayments.Where(p => p.UserId == user.UserId).ToList();
                
                // Calculate meal charges based on attendance
                decimal mealCharges = 0;
                int breakfastCount = 0, lunchCount = 0, dinnerCount = 0;
                
                foreach (var att in userAttendances)
                {
                    if (att.IsBreakfastPresent && att.BreakfastDishPlan != null)
                    {
                        mealCharges += att.BreakfastDishPlan.Price;
                        breakfastCount++;
                    }
                    if (att.IsLunchPresent && att.LunchDishPlan != null)
                    {
                        mealCharges += att.LunchDishPlan.Price;
                        lunchCount++;
                    }
                    if (att.IsDinnerPresent && att.DinnerDishPlan != null)
                    {
                        mealCharges += att.DinnerDishPlan.Price;
                        dinnerCount++;
                    }
                }
                
                // Water charges (fixed per period)
                decimal waterCharges = currentPeriod.FixedWaterCharge;
                
                // Tea charges - use per-user actual consumption from TeaEntries
                var userTeaEntries = allTeaEntries.Where(t => t.UserId == user.UserId).ToList();
                int userTeaCups = userTeaEntries.Sum(t => t.Cups);
                decimal teaCharges = userTeaCups * currentPeriod.TeaPricePerCup;
                
                var summary = new MemberPaymentSummary
                {
                    User = user,
                    MealCharges = mealCharges,
                    WaterCharges = waterCharges,
                    TeaCharges = teaCharges,
                    TotalPaid = userPayments.Sum(p => p.Amount),
                    BreakfastCount = breakfastCount,
                    LunchCount = lunchCount,
                    DinnerCount = dinnerCount,
                    TeaCups = userTeaCups,
                    Payments = userPayments
                };
                
                viewModel.MemberSummaries.Add(summary);
            }

            // Calculate totals (only approved/completed payments)
            viewModel.TotalPeriodCharges = viewModel.MemberSummaries.Sum(s => s.TotalCharges);
            viewModel.TotalPeriodPaid = viewModel.MemberSummaries.Sum(s => s.TotalPaid);
            viewModel.TotalCashPayments = allPayments.Where(p => p.PaymentMethod == "Cash").Sum(p => p.Amount);
            viewModel.TotalStripePayments = allPayments.Where(p => p.PaymentMethod == "Stripe").Sum(p => p.Amount);
            viewModel.TotalBankTransferPayments = allPayments.Where(p => p.PaymentMethod == "Bank Transfer").Sum(p => p.Amount);
            viewModel.TotalOtherPayments = allPayments.Where(p => p.PaymentMethod != "Cash" && p.PaymentMethod != "Stripe" && p.PaymentMethod != "Bank Transfer").Sum(p => p.Amount);
            viewModel.TotalPaymentCount = allPayments.Count;

            return View(viewModel);
        }

        // Admin view for pending payment approvals
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PendingApprovals(int? periodId)
        {
            var periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
            ViewBag.Periods = periods;

            MessPeriod? currentPeriod = null;
            if (periodId.HasValue)
            {
                currentPeriod = periods.FirstOrDefault(p => p.PeriodId == periodId);
            }
            currentPeriod ??= periods.FirstOrDefault(p => p.IsActive) ?? periods.FirstOrDefault();
            ViewBag.CurrentPeriod = currentPeriod;

            // Exclude "Auto" payments - these are auto-generated attendance charges, not actual payments
            // Only show payments submitted by users (Cash, Bank Transfer, etc.) that need admin approval
            IQueryable<Payment> query = _context.Payments
                .Include(p => p.User)
                .Include(p => p.MessPeriod)
                .Where(p => p.Status == PaymentStatus.Pending && p.PaymentMethod != "Auto");

            if (currentPeriod != null)
            {
                query = query.Where(p => p.PeriodId == currentPeriod.PeriodId);
            }

            var pendingPayments = await query.OrderBy(p => p.PaymentDate).ToListAsync();
            return View(pendingPayments);
        }

        // Admin action to approve a payment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id)
        {
            var payment = await _context.Payments
                .Include(p => p.User)
                .Include(p => p.MessPeriod)
                .FirstOrDefaultAsync(p => p.PaymentId == id);
            if (payment == null) return NotFound();

            // Get admin user id
            var adminUsername = User.Identity?.Name;
            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Username == adminUsername);

            payment.Status = PaymentStatus.Approved;
            payment.ApprovedByUserId = admin?.UserId;
            payment.ApprovedAt = DateTime.Now;
            payment.RejectionReason = null;

            _context.Update(payment);
            await _context.SaveChangesAsync();

            // Send payment approval email if user has email
            if (payment.User != null && !string.IsNullOrWhiteSpace(payment.User.Email))
            {
                try
                {
                    var periodName = payment.MessPeriod?.PeriodName ?? "N/A";
                    await _emailService.SendPaymentApprovalEmailAsync(
                        payment.User.Email,
                        payment.User.FullName,
                        payment.Amount,
                        payment.PaymentMethod,
                        periodName,
                        payment.ApprovedAt ?? DateTime.Now
                    );
                    _logger.LogInformation("Payment approval email sent to {Email} for payment {PaymentId}", payment.User.Email, payment.PaymentId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send payment approval email to {Email}", payment.User.Email);
                }
            }

            TempData["SuccessMessage"] = "Payment approved successfully!";
            return RedirectToAction(nameof(PendingApprovals));
        }

        // Admin action to reject a payment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reject(int id, string rejectionReason)
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment == null) return NotFound();

            // Get admin user id
            var adminUsername = User.Identity?.Name;
            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Username == adminUsername);

            payment.Status = PaymentStatus.Rejected;
            payment.ApprovedByUserId = admin?.UserId;
            payment.ApprovedAt = DateTime.Now;
            payment.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) 
                ? "Payment rejected by admin" 
                : rejectionReason;

            _context.Update(payment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Payment rejected.";
            return RedirectToAction(nameof(PendingApprovals));
        }

        // User's payment history
        public async Task<IActionResult> MyPayments(int? periodId)
        {
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();

            // Get all periods for selection
            var allPeriods = await _context.MessPeriods
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();
            ViewBag.AllPeriods = allPeriods;

            // Get selected period or active period
            MessPeriod? selectedPeriod = null;
            if (periodId.HasValue)
            {
                selectedPeriod = allPeriods.FirstOrDefault(p => p.PeriodId == periodId);
            }
            selectedPeriod ??= allPeriods.FirstOrDefault(p => p.IsActive) ?? allPeriods.FirstOrDefault();
            ViewBag.SelectedPeriod = selectedPeriod;

            // Get user's actual payments (not Auto charges)
            var payments = await _context.Payments
                .Include(p => p.MessPeriod)
                .Include(p => p.ApprovedByUser)
                .Where(p => p.UserId == user.UserId && p.PaymentMethod != "Auto")
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            // Get all user attendance records
            var attendances = await _context.Attendances
                .Where(a => a.UserId == user.UserId)
                .Include(a => a.BreakfastDishPlan)
                .Include(a => a.LunchDishPlan)
                .Include(a => a.DinnerDishPlan)
                .OrderByDescending(a => a.Date)
                .ToListAsync();
            ViewBag.Attendances = attendances;

            // Get all tea entries
            var teaEntries = await _context.TeaEntries
                .Where(t => t.UserId == user.UserId)
                .Include(t => t.MessPeriod)
                .OrderByDescending(t => t.Date)
                .ToListAsync();
            ViewBag.TeaEntries = teaEntries;

            // Calculate summary for selected period
            if (selectedPeriod != null)
            {
                ViewBag.RemainingBalance = await CalculateRemainingBalance(user.UserId, selectedPeriod.PeriodId);
                ViewBag.CurrentPeriod = selectedPeriod;
                
                // Pending amount (submitted but not yet approved, exclude Auto charges)
                var pendingAmount = payments
                    .Where(p => p.PeriodId == selectedPeriod.PeriodId && p.Status == PaymentStatus.Pending && p.PaymentMethod != "Auto")
                    .Sum(p => p.Amount);
                ViewBag.PendingAmount = pendingAmount;

                // Calculate period charges breakdown
                var periodAttendances = attendances.Where(a => a.Date >= selectedPeriod.StartDate && a.Date <= selectedPeriod.EndDate).ToList();
                var periodTeaEntries = teaEntries.Where(t => t.Date >= selectedPeriod.StartDate && t.Date <= selectedPeriod.EndDate).ToList();

                decimal mealCharges = 0;
                int breakfastCount = 0, lunchCount = 0, dinnerCount = 0;
                foreach (var att in periodAttendances)
                {
                    if (att.IsBreakfastPresent && att.BreakfastDishPlan != null)
                    {
                        mealCharges += att.BreakfastDishPlan.Price;
                        breakfastCount++;
                    }
                    if (att.IsLunchPresent && att.LunchDishPlan != null)
                    {
                        mealCharges += att.LunchDishPlan.Price;
                        lunchCount++;
                    }
                    if (att.IsDinnerPresent && att.DinnerDishPlan != null)
                    {
                        mealCharges += att.DinnerDishPlan.Price;
                        dinnerCount++;
                    }
                }

                int teaCups = periodTeaEntries.Sum(t => t.Cups);
                decimal teaCharges = teaCups * selectedPeriod.TeaPricePerCup;
                decimal waterCharges = selectedPeriod.FixedWaterCharge;

                ViewBag.MealCharges = mealCharges;
                ViewBag.TeaCharges = teaCharges;
                ViewBag.WaterCharges = waterCharges;
                ViewBag.TotalCharges = mealCharges + teaCharges + waterCharges;
                ViewBag.BreakfastCount = breakfastCount;
                ViewBag.LunchCount = lunchCount;
                ViewBag.DinnerCount = dinnerCount;
                ViewBag.TeaCups = teaCups;

                // Total paid for this period
                var totalPaid = payments
                    .Where(p => p.PeriodId == selectedPeriod.PeriodId && (p.Status == PaymentStatus.Approved || p.Status == PaymentStatus.Completed))
                    .Sum(p => p.Amount);
                ViewBag.TotalPaid = totalPaid;
            }

            ViewBag.User = user;
            return View(payments);
        }

        // Member payment detail view (Admin only)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> MemberDetail(int userId, int? periodId, DateTime? date)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
            
            MessPeriod? currentPeriod = null;
            if (periodId.HasValue)
            {
                currentPeriod = periods.FirstOrDefault(p => p.PeriodId == periodId);
            }
            currentPeriod ??= periods.FirstOrDefault(p => p.IsActive) ?? periods.FirstOrDefault();

            // Set selected date for calendar
            var selectedDate = date ?? DateTime.Today;
            ViewBag.SelectedDate = selectedDate;

            var viewModel = new MemberPaymentDetailViewModel
            {
                User = user,
                Period = currentPeriod,
                AllPeriods = periods
            };

            if (currentPeriod == null) return View(viewModel);

            var start = currentPeriod.StartDate;
            var end = currentPeriod.EndDate;

            // Get user's attendances with dish details
            var attendances = await _context.Attendances
                .Where(a => a.UserId == userId && a.Date >= start && a.Date <= end)
                .Include(a => a.BreakfastDishPlan)
                .Include(a => a.LunchDishPlan)
                .Include(a => a.DinnerDishPlan)
                .OrderBy(a => a.Date)
                .ToListAsync();

            decimal mealCharges = 0;
            foreach (var att in attendances)
            {
                var detail = new AttendanceChargeDetail
                {
                    Date = att.Date
                };

                if (att.IsBreakfastPresent && att.BreakfastDishPlan != null)
                {
                    detail.BreakfastDish = att.BreakfastDishPlan.DishName;
                    detail.BreakfastCharge = att.BreakfastDishPlan.Price;
                    mealCharges += att.BreakfastDishPlan.Price;
                }
                if (att.IsLunchPresent && att.LunchDishPlan != null)
                {
                    detail.LunchDish = att.LunchDishPlan.DishName;
                    detail.LunchCharge = att.LunchDishPlan.Price;
                    mealCharges += att.LunchDishPlan.Price;
                }
                if (att.IsDinnerPresent && att.DinnerDishPlan != null)
                {
                    detail.DinnerDish = att.DinnerDishPlan.DishName;
                    detail.DinnerCharge = att.DinnerDishPlan.Price;
                    mealCharges += att.DinnerDishPlan.Price;
                }

                viewModel.AttendanceDetails.Add(detail);
            }

            // Tea calculation - use per-user actual consumption from TeaEntries
            var userTeaEntries = await _context.TeaEntries
                .Where(t => t.UserId == userId && (t.PeriodId == currentPeriod.PeriodId || (t.Date >= start && t.Date <= end)))
                .ToListAsync();
            viewModel.TeaCups = userTeaEntries.Sum(t => t.Cups);

            viewModel.MealCharges = mealCharges;
            viewModel.WaterCharges = currentPeriod.FixedWaterCharge;
            viewModel.TeaCharges = viewModel.TeaCups * currentPeriod.TeaPricePerCup;

            // Get payment history (all statuses for admin view)
            viewModel.PaymentHistory = await _context.Payments
                .Where(p => p.UserId == userId && p.PeriodId == currentPeriod.PeriodId)
                .Include(p => p.ApprovedByUser)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            // Only count approved/completed payments as paid
            viewModel.TotalPaid = viewModel.PaymentHistory
                .Where(p => p.Status == PaymentStatus.Approved || p.Status == PaymentStatus.Completed)
                .Sum(p => p.Amount);

            return View(viewModel);
        }

        // All payment records list (Admin only)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Records(int? periodId, string? status)
        {
            var periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
            ViewBag.Periods = periods;
            
            MessPeriod? currentPeriod = null;
            if (periodId.HasValue)
            {
                currentPeriod = periods.FirstOrDefault(p => p.PeriodId == periodId);
            }
            currentPeriod ??= periods.FirstOrDefault(p => p.IsActive) ?? periods.FirstOrDefault();
            ViewBag.CurrentPeriod = currentPeriod;
            ViewBag.StatusFilter = status;

            IQueryable<Payment> query = _context.Payments
                .Include(p => p.User)
                .Include(p => p.MessPeriod)
                .Include(p => p.ApprovedByUser);

            if (currentPeriod != null)
            {
                query = query.Where(p => p.PeriodId == currentPeriod.PeriodId);
            }

            // Filter by status if specified
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<PaymentStatus>(status, out var statusEnum))
            {
                query = query.Where(p => p.Status == statusEnum);
            }

            var items = await query.OrderByDescending(p => p.PaymentDate).ToListAsync();
            return View(items);
        }

        // User submits a new payment
        public async Task<IActionResult> Create(int? periodId)
        {
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return Unauthorized();

            var periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
            ViewBag.Periods = periods;
            
            var payment = new Payment { UserId = user.UserId };
            
            if (periodId.HasValue) 
                payment.PeriodId = periodId.Value;
            else
            {
                var activePeriod = periods.FirstOrDefault(p => p.IsActive);
                if (activePeriod != null) payment.PeriodId = activePeriod.PeriodId;
            }

            // Calculate remaining balance
            if (payment.PeriodId.HasValue)
            {
                var remaining = await CalculateRemainingBalance(user.UserId, payment.PeriodId.Value);
                ViewBag.RemainingBalance = remaining;
            }

            ViewBag.CurrentUser = user;
            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PeriodId,Amount,PaymentMethod,ReferenceNumber,Remarks")] Payment payment)
        {
            // User can only submit payment for themselves
            var username = User.Identity?.Name;
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (currentUser == null) return Unauthorized();
            
            payment.UserId = currentUser.UserId;

            if (!ModelState.IsValid) 
            {
                ViewBag.Periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
                ViewBag.CurrentUser = currentUser;
                return View(payment);
            }

            // Check verification status - payment cannot be submitted until all meals and tea are verified
            if (payment.PeriodId.HasValue)
            {
                var verificationStatus = await GetVerificationStatus(payment.UserId, payment.PeriodId.Value);
                if (!verificationStatus.AllVerified)
                {
                    var errorMessage = "Payment cannot be submitted until all items are verified. ";
                    if (verificationStatus.PendingMeals > 0)
                        errorMessage += $"You have {verificationStatus.PendingMeals} meal(s) pending verification. ";
                    if (verificationStatus.PendingTea > 0)
                        errorMessage += $"You have {verificationStatus.PendingTea} tea entry(s) pending verification.";
                    
                    ModelState.AddModelError("", errorMessage);
                    ViewBag.Periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
                    ViewBag.CurrentUser = currentUser;
                    ViewBag.VerificationError = true;
                    ViewBag.PendingMeals = verificationStatus.PendingMeals;
                    ViewBag.PendingTea = verificationStatus.PendingTea;
                    return View(payment);
                }
            }

            // Handle Stripe payment - auto complete on success
            if (payment.PaymentMethod == "Stripe")
            {
                // Stripe has a minimum amount requirement of 150 PKR
                if (payment.Amount < 150)
                {
                    ModelState.AddModelError("Amount", "Minimum amount for card payments is Rs. 150. For smaller amounts, please use Cash or Bank Transfer.");
                    ViewBag.Periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
                    ViewBag.CurrentUser = currentUser;
                    return View(payment);
                }
                
                // Create a Stripe Checkout Session and redirect
                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                UnitAmountDecimal = payment.Amount * 100,
                                Currency = "pkr",
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = "Mess Payment"
                                }
                            },
                            Quantity = 1
                        }
                    },
                    Mode = "payment",
                    SuccessUrl = Url.Action("StripeSuccess", "Payment", null, Request.Scheme),
                    CancelUrl = Url.Action("MyPayments", "Payment", null, Request.Scheme)
                };
                var service = new SessionService();
                var session = service.Create(options);
                
                // Stripe payments are auto-completed (verified by webhook)
                payment.StripePaymentId = session.Id;
                payment.PaymentDate = DateTime.Now;
                payment.Status = PaymentStatus.Completed; // Stripe is auto-verified
                
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();
                return Redirect(session.Url);
            }

            // Cash, Bank Transfer, Other - set to Pending for admin approval
            payment.PaymentDate = DateTime.Now;
            payment.Status = PaymentStatus.Pending;
            
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Payment submitted successfully! It will be reflected in your balance once approved by admin.";
            return RedirectToAction(nameof(MyPayments));
        }

        // API endpoint to get remaining balance for a user/period combo
        [HttpGet]
        public async Task<IActionResult> GetRemainingBalance(int userId, int periodId)
        {
            // Regular users can only check their own balance
            if (!User.IsInRole("Admin"))
            {
                var username = User.Identity?.Name;
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (currentUser == null || currentUser.UserId != userId)
                    return Unauthorized();
            }

            var remaining = await CalculateRemainingBalance(userId, periodId);
            var verificationStatus = await GetVerificationStatus(userId, periodId);
            return Json(new { 
                remainingBalance = remaining,
                allVerified = verificationStatus.AllVerified,
                pendingMeals = verificationStatus.PendingMeals,
                pendingTea = verificationStatus.PendingTea
            });
        }

        /// <summary>
        /// Check if user has all meals and tea entries verified for a period
        /// </summary>
        private async Task<(bool AllVerified, int PendingMeals, int PendingTea)> GetVerificationStatus(int userId, int periodId)
        {
            var period = await _context.MessPeriods.FindAsync(periodId);
            if (period == null) return (true, 0, 0);

            var start = period.StartDate;
            var end = period.EndDate;

            // Check attendance verification
            var attendances = await _context.Attendances
                .Where(a => a.UserId == userId && a.Date >= start && a.Date <= end)
                .ToListAsync();

            int pendingMeals = attendances.Sum(a => a.PendingVerificationCount);

            // Check tea verification
            var teaEntries = await _context.TeaEntries
                .Where(t => t.UserId == userId && (t.PeriodId == periodId || (t.Date >= start && t.Date <= end)))
                .ToListAsync();

            int pendingTea = teaEntries.Count(t => t.Cups > 0 && !t.VerifiedByUser);

            bool allVerified = pendingMeals == 0 && pendingTea == 0;

            return (allVerified, pendingMeals, pendingTea);
        }

        private async Task<decimal> CalculateRemainingBalance(int userId, int periodId)
        {
            var period = await _context.MessPeriods.FindAsync(periodId);
            if (period == null) return 0;

            var start = period.StartDate;
            var end = period.EndDate;

            // Calculate meal charges
            var attendances = await _context.Attendances
                .Where(a => a.UserId == userId && a.Date >= start && a.Date <= end)
                .Include(a => a.BreakfastDishPlan)
                .Include(a => a.LunchDishPlan)
                .Include(a => a.DinnerDishPlan)
                .ToListAsync();

            decimal mealCharges = 0;
            foreach (var att in attendances)
            {
                if (att.IsBreakfastPresent && att.BreakfastDishPlan != null)
                    mealCharges += att.BreakfastDishPlan.Price;
                if (att.IsLunchPresent && att.LunchDishPlan != null)
                    mealCharges += att.LunchDishPlan.Price;
                if (att.IsDinnerPresent && att.DinnerDishPlan != null)
                    mealCharges += att.DinnerDishPlan.Price;
            }

            // Water charges
            decimal waterCharges = period.FixedWaterCharge;

            // Tea charges - use per-user actual consumption from TeaEntries
            var userTeaEntries = await _context.TeaEntries
                .Where(t => t.UserId == userId && (t.PeriodId == periodId || (t.Date >= start && t.Date <= end)))
                .ToListAsync();
            int userTeaCups = userTeaEntries.Sum(t => t.Cups);
            decimal teaCharges = userTeaCups * period.TeaPricePerCup;

            decimal totalCharges = mealCharges + waterCharges + teaCharges;

            // Only count APPROVED/COMPLETED payments
            var totalPaid = await _context.Payments
                .Where(p => p.UserId == userId && p.PeriodId == periodId && 
                       (p.Status == PaymentStatus.Approved || p.Status == PaymentStatus.Completed))
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            return totalCharges - totalPaid;
        }

        public IActionResult StripeSuccess()
        {
            TempData["SuccessMessage"] = "Card payment completed successfully!";
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Webhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            try
            {
                var stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], "");
                if (stripeEvent.Type == Events.CheckoutSessionCompleted)
                {
                    var session = stripeEvent.Data.Object as Session;
                    if (session != null)
                    {
                        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.StripePaymentId == session.Id);
                        if (payment != null)
                        {
                            payment.Status = PaymentStatus.Completed;
                            _context.Payments.Update(payment);
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                return Ok();
            }
            catch (Exception)
            {
                return BadRequest();
            }
        }

        // GET: Payment/Details/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var payment = await _context.Payments
                .Include(p => p.User)
                .Include(p => p.MessPeriod)
                .Include(p => p.Attendance)
                .Include(p => p.ApprovedByUser)
                .FirstOrDefaultAsync(p => p.PaymentId == id);
            if (payment == null) return NotFound();
            return View(payment);
        }

        // GET: Payment/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var payment = await _context.Payments.FindAsync(id);
            if (payment == null) return NotFound();
            ViewBag.Users = await _context.Users.Where(u => u.IsActive).ToListAsync();
            ViewBag.Periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("PaymentId,UserId,PeriodId,Amount,PaymentDate,PaymentMethod,StripePaymentId,ReferenceNumber,Remarks,Status,ApprovedByUserId,ApprovedAt,RejectionReason,AttendanceId")] Payment payment)
        {
            if (id != payment.PaymentId) return NotFound();
            if (!ModelState.IsValid) {
                ViewBag.Users = await _context.Users.Where(u => u.IsActive).ToListAsync();
                ViewBag.Periods = await _context.MessPeriods.OrderByDescending(p => p.StartDate).ToListAsync();
                return View(payment);
            }
            try
            {
                _context.Update(payment);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Payment updated successfully!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Payments.Any(p => p.PaymentId == id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Records));
        }

        // GET: Payment/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var payment = await _context.Payments
                .Include(p => p.User)
                .Include(p => p.MessPeriod)
                .Include(p => p.Attendance)
                .FirstOrDefaultAsync(p => p.PaymentId == id);
            if (payment == null) return NotFound();
            return View(payment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment != null) _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Payment deleted successfully!";
            return RedirectToAction(nameof(Records));
        }
    }
}
