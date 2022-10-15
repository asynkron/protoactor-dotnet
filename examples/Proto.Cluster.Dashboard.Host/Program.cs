using Google.Protobuf.WellKnownTypes;
using MudBlazor.Services;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Dashboard;
using Proto.Cluster.Partition;
using Proto.Cluster.Seed;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using Proto.Remote.HealthChecks;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddLogging(x => x.AddConsole());
builder.Services.AddProtoActorDashboard(new DashboardSettings
{
    LogSearchPattern = "",
    TraceSearchPattern = ""
});

builder.Services.AddProtoCluster("MyCluster", port: 8090,
    configureRemote: r => r.WithRemoteDiagnostics(true),
    configureCluster: c => c);

builder.Services.AddHealthChecks().AddCheck<ActorSystemHealthCheck>("proto", null, new[] { "ready", "live" });


// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
