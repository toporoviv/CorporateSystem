using CorporateSystem.ApiGateway.Api.Middlewares;
using CorporateSystem.ApiGateway.Services.Extensions;
using CorporateSystem.ApiGateway.Services.Options;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Services.AddOcelot();
builder.Services.AddApiGatewayServices();
builder.Services.Configure<AuthMicroserviceOptions>(builder.Configuration.GetSection("AuthMicroserviceOptions"));
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8003);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientCors", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("ClientCors");

app.UseMiddleware<AuthenticationMiddleware>();
app.UseWebSockets();
await app.UseOcelot();
app.Run();
