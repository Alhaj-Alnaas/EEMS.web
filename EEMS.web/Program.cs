
//using Core.Entities;
//using Core.Interfaces.Services;
//using DataAccess;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.EntityFrameworkCore;
//using Services;
//using ACS.Web.Providers; // Where IUserProvider and UserProvider live

//var builder = WebApplication.CreateBuilder(args);

//// -----------------------------------------
//// 1️⃣ Database connection
//// -----------------------------------------
//builder.Services.AddDbContext<DataContext>(options =>
//    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//// -----------------------------------------
//// 2️⃣ ASP.NET Core Identity with custom User
//// -----------------------------------------
//builder.Services.AddIdentity<User, IdentityRole>(options =>
//{
//    options.Password.RequireDigit = false;
//    options.Password.RequireNonAlphanumeric = false;
//    options.Password.RequiredLength = 6;
//    options.User.RequireUniqueEmail = true;
//})
//.AddEntityFrameworkStores<DataContext>()
//.AddDefaultTokenProviders();

//// -----------------------------------------
//// 3️⃣ Application Services (Dependency Injection)
//// -----------------------------------------
//builder.Services.AddTransient(typeof(IGates<>), typeof(GateServices<>));
//builder.Services.AddTransient<IUserProvider, UserProvider>();

//// -----------------------------------------
//// 4️⃣ MVC + Razor Pages (needed for Identity UI)
//// -----------------------------------------
//builder.Services.AddControllersWithViews();
//builder.Services.AddRazorPages();

//// -----------------------------------------
//// 5️⃣ Build App
//// -----------------------------------------
//var app = builder.Build();

//// -----------------------------------------
//// 6️⃣ Middleware pipeline
//// -----------------------------------------
//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Home/Error");
//    app.UseHsts();
//}

//app.UseHttpsRedirection();
//app.UseStaticFiles();

//app.UseRouting();

//app.UseAuthentication(); // MUST be before Authorization
//app.UseAuthorization();

//// -----------------------------------------
//// 7️⃣ Endpoints mapping
//// -----------------------------------------
//app.MapControllerRoute(
//    name: "default",
//   // pattern: "{controller=Home}/{action=Index}/{id?}");
//pattern: "{controller=Gate}/{action=Index}/{id?}");

//app.MapRazorPages(); // Needed for Identity UI pages like /Account/Login

//// -----------------------------------------
//app.Run();





















using ACS.Web.Providers;
using Core.Entities;
using Core.Interfaces.Services;
using DataAccess;
using DataAccess.Repositories;
using DataAccess.UnitOfWork;
using EEMS.Core.Interfaces.Repositories;
using EEMS.Core.Interfaces.UnitOfWork;
using EEMS.web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Services;

var builder = WebApplication.CreateBuilder(args);

// add this line by Alnaas to test
// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();


//builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
//    .AddEntityFrameworkStores<ApplicationDbContext>();
//builder.Services.AddControllersWithViews();

// Add DbContext
builder.Services.AddDbContext<DataContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity
builder.Services.AddIdentity<User, IdentityRole>()
    .AddEntityFrameworkStores<DataContext>()
    .AddDefaultTokenProviders();

// Your DI registrations
//builder.Services.AddScoped(IUnitOfWork, UnitOfWork<>);
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

builder.Services.AddTransient<IUserProvider, UserProvider>();
builder.Services.AddTransient<IGates, GateServices>();
builder.Services.AddTransient<IPermitType, PermitTypeServices>();

builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
   // pattern: "{controller=Gate}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
