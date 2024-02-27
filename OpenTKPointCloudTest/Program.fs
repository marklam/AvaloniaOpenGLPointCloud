namespace OpenTKPointCloudTest
open System

module Program =

    [<EntryPoint; STAThread>]
    let main argv =

        let provider = OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext()
        OpenTK.Graphics.GLLoader.LoadBindings(provider)

        use w = new Window()
        w.Run()
        0

