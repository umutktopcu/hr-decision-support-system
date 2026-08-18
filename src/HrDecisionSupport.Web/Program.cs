using HrDecisionSupport.Application;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddHttpClient<IMlPredictionService, HrDecisionSupport.Infrastructure.MlPredictionService>();
builder.Services.AddInfrastructure(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgreSql")
        ?? throw new InvalidOperationException(
            "PostgreSQL connection string is not configured."),
        npgsqlOptions => npgsqlOptions.UseVector()),
    builder.Configuration);
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");


app.Run();
