    using Microsoft.AspNetCore.Mvc;

    namespace MessManagement.Controllers
    {
        public class HomeController : Controller
        {
            /// <summary>
            /// Index action - demonstrates Session usage
            /// Session stores user visit count across requests
            /// </summary>
            public IActionResult Index()
            {
                // Session demonstration: Track visit count
                int visitCount = HttpContext.Session.GetInt32("VisitCount") ?? 0;
                visitCount++;
                HttpContext.Session.SetInt32("VisitCount", visitCount);
                
                // Store last visit time in session
                HttpContext.Session.SetString("LastVisit", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                
                // Pass session data to view via ViewBag
                ViewBag.VisitCount = visitCount;
                ViewBag.LastVisit = HttpContext.Session.GetString("LastVisit");
                
                return View();
            }

            public IActionResult Privacy()
            {
                return View();
            }

            public IActionResult Error()
            {
                return View();
            }
            
            /// <summary>
            /// API endpoint to get session data (async)
            /// </summary>
            [HttpGet]
            public async Task<IActionResult> GetSessionDataAsync()
            {
                await Task.CompletedTask; // Async demonstration
                
                var visitCount = HttpContext.Session.GetInt32("VisitCount") ?? 0;
                var lastVisit = HttpContext.Session.GetString("LastVisit");
                
                return Json(new { 
                    success = true, 
                    visitCount, 
                    lastVisit,
                    sessionId = HttpContext.Session.Id 
                });
            }
            
            /// <summary>
            /// Clear session data
            /// </summary>
            [HttpPost]
            public IActionResult ClearSession()
            {
                HttpContext.Session.Clear();
                return Json(new { success = true, message = "Session cleared" });
            }
            
            /// <summary>
            /// Switch view mode for admin users between "admin" and "member" view
            /// Stores preference in session and redirects back to the referring page
            /// </summary>
            [HttpGet]
            public IActionResult SwitchViewMode(string mode)
            {
                // Validate user is admin
                if (!User.IsInRole("Admin"))
                {
                    return RedirectToAction("Index");
                }
                
                // Validate mode parameter
                if (mode != "admin" && mode != "member")
                {
                    mode = "admin";
                }
                
                // Store view mode preference in session
                HttpContext.Session.SetString("ViewMode", mode);
                
                // Redirect back to the referring page or home
                var returnUrl = Request.Headers["Referer"].ToString();
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                
                return RedirectToAction("Index");
            }
        }
    }

