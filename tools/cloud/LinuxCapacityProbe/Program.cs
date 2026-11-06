using System.Runtime.InteropServices;

var builder = WebApplication.CreateSlimBuilder(args);
var app = builder.Build();
var processInstance = Guid.NewGuid().ToString("N");

app.MapGet("/", () => Results.Text("MiniPainterHub Linux capacity probe"));
app.MapGet("/healthz", () => Results.Json(new
{
    status = "ok",
    probe = "MiniPainterHub.LinuxCapacityProbe",
    runtime = RuntimeInformation.FrameworkDescription,
    operatingSystem = RuntimeInformation.OSDescription,
    isLinux = OperatingSystem.IsLinux(),
    processInstance
}));

app.Run();
