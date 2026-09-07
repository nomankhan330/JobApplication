using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using BusinessLogic.Services;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddControllers()
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
});

builder.Services.AddScoped<IDatabaseObject, DatabaseObject>(provider =>
{
    var sessionHelper = provider.GetRequiredService<ISessionHelper>();
    //var logs = provider.GetRequiredService<ILogs>();

    // Assuming "AppCon" is the name of your connection string in appsettings.json
    var connectionString = builder.Configuration.GetConnectionString("AppCon");

    return new DatabaseObject(connectionString, sessionHelper);
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.MaxAge = TimeSpan.FromHours(24);
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddScoped<BusinessLogic.Models.AppDbContext>();
builder.Services.AddDbContext<BusinessLogic.Models.AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AppCon"),
        sqlServerOptions => sqlServerOptions.CommandTimeout(1000)
    )
);

builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue; // ~2GB
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = int.MaxValue;
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ISessionHelper, SessionHelper>();
builder.Services.AddScoped<ISBService, SBService>();
builder.Services.AddScoped<ILogs, BusinessLogic.Services.Logs>();
builder.Services.AddScoped<IAccount, Account>();
builder.Services.AddScoped<IDropdown, Dropdown>();
builder.Services.AddScoped<ICostCenter, CostCenterService>();
builder.Services.AddScoped<ICompany, CompanyService>();
builder.Services.AddScoped<ICustomer, CustomerService>();

builder.Services.AddScoped<IPol, PolService>();
builder.Services.AddScoped<IPod, PodService>();
builder.Services.AddScoped<IDeliveryCity, DeliveryCityService>();
builder.Services.AddScoped<IJobImportMaster, JobImportMasterService>();
builder.Services.AddScoped<ISalesInvoice, SalesInvoiceService>();
builder.Services.AddScoped<IPurchaseInvoice, PurchaseInvoiceService>();

builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Login}/{id?}");

app.Run();
