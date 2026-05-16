using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Security;

var builder = WebApplication.CreateBuilder(args);

AdminBindPolicy.Apply(builder);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProxyDataPlane(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<AdminAuthenticationMiddleware>();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
