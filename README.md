# Mess Management System

A simple ASP.NET Core MVC application demonstrating a Mess Management System with Users, Dish Plan, Mess Period, Attendance, Tea Records, Payments, and Reports.

## Features
- Admin and User roles
- Admin can create/delete/manage Users, DishPlan, MessPeriod, TeaRecord, Payments
- Admin can mark attendance & view reports
- Users can view their own attendance and verify it
- Stripe Checkout integration (demo) and webhook handling
- Validation (server-side and client-side with jQuery Unobtrusive validation)

## Getting Started
1. Open the `MessManagement.sln` (or open the project folder) in Visual Studio 2022.
2. Update the connection string in `appsettings.json` to point to your SQL Server.
3. Optionally update the Stripe config keys in `appsettings.json`.
4. Build the project to restore NuGet packages.
5. Run the application (F5). On first run, the database will be created and seeded with a default admin account:
   - Username: `admin`
   - Password: `Admin@123`

6. Use the admin account to create members and manage the mess.

## Notes
- The app uses cookie-based authentication.
- For production, do not store Stripe keys in `appsettings.json` — use secure secret storage.
- Webhook verification is minimal here and should be validated using the Stripe webhook secret in production.

## Dev
- To apply EF Core migrations, run in the Package Manager Console:

```powershell
Add-Migration Initial
Update-Database
```

- If you prefer DB-First, use `Scaffold-DbContext` to recreate models from your database.

## License
This project is an educational example for a semester project.