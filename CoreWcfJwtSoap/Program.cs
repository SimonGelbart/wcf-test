using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWcfJwtSoap;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddGreetingAuthorization();
builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<GreetingMessageStore>();

var app = builder.Build();
app.UseAuthentication();
app.UseServiceModel(services => services.MapGreetingEndpoints());

var metadata = app.Services.GetRequiredService<ServiceMetadataBehavior>();
metadata.HttpsGetEnabled = true;

// CoreWCF runs before endpoint routing authorization fallback policies.
app.UseAuthorization();
app.Run();
