using Microsoft.EntityFrameworkCore;
using SharpTalk.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // app.UseSwaggerUI(); // If using Swashbuckle, but MapOpenApi is for the new built-in support
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
