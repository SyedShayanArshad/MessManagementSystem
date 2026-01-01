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
        }
    }

