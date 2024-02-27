namespace PointCloudTest.Desktop
open System
open Avalonia
open Avalonia.ReactiveUI
open PointCloudTest

module Program =

    [<CompiledName "BuildAvaloniaApp">]
    let buildAvaloniaApp () =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace(areas = Array.empty)
            .UseReactiveUI()
            .With(Win32PlatformOptions(RenderingMode = [Win32RenderingMode.Wgl]))
            //.With(Win32PlatformOptions(RenderingMode = [Win32RenderingMode.AngleEgl]))

    [<EntryPoint; STAThread>]
    let main argv =
        buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
