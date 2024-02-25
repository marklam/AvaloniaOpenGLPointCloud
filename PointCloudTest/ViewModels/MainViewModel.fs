namespace PointCloudTest.ViewModels

open OpenTK.Mathematics
open PointCloudTest

type MainViewModel() =
    inherit ViewModelBase()

    let r = System.Random()
    let generateCloud side z =
        let step = 2f / single side
        let steps = [ -1f .. step .. 1f ]
        [|
            for x in steps do
                for y in steps do
                    let i = r.NextSingle()
                    yield { Position = Vector3(x, y, z); Intensity = i }
        |]

    let pcvm =
        printfn "Creating PointCloudViewModel"
        { Cloud1 = generateCloud 6325 -1f; Cloud2 = generateCloud 5478 1f }

    member this.PointCloudViewModel = pcvm
