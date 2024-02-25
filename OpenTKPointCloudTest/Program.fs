namespace OpenTKPointCloudTest
open System

module Program =

    [<EntryPoint; STAThread>]
    let main argv =
        use w = new Window()
        w.Run()
        0

