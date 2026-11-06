using System.Runtime.CompilerServices;
using MiniPainterHub.Server.Imaging;

namespace MiniPainterHub.Server.Tests.Infrastructure;

internal static class ImageProcessingTestSetup
{
    // Match Program.Main: load the production policy before any native image fixture.
    [ModuleInitializer]
    internal static void Initialize() => RasterImageDecoder.Initialize();
}
