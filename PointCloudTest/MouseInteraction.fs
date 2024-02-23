namespace PointCloudTest

open System.Numerics

module MouseInteraction =
    type DragType = | Rotate

    type DragMovement =
        {
            ControlSize : Avalonia.Size
            From        : Avalonia.Point
            To          : Avalonia.Point
            Type        : DragType
        }

    module DragMovement =
        let dx (dm : DragMovement) = (dm.To.X - dm.From.X) / dm.ControlSize.Width  |> single
        let dy (dm : DragMovement) = (dm.To.Y - dm.From.Y) / dm.ControlSize.Height |> single

    type TransformInteraction =
    | Drag of DragMovement

    type ModelTransform =
        {
            Center         : Vector3
            Translation    : Matrix4x4
            Rotation       : Matrix4x4
            Scaling        : Matrix4x4
            PanSensitivity : single
        }

    module ModelTransform =
        let matrix modelTransform =
            modelTransform.Scaling * modelTransform.Rotation * modelTransform.Translation

        let normMatrix modelTransform =
            match Matrix4x4.Invert(modelTransform.Rotation) with
            | true, inv ->
                Matrix4x4.Transpose(inv)
            | false, _ ->
                Matrix4x4.Identity

        let defaults =
            {
                Center         = Vector3.Zero
                Translation    = Matrix4x4.Identity
                Rotation       = Matrix4x4.Identity
                Scaling        = Matrix4x4.Identity
                PanSensitivity = 2.0f
            }

        let applyInteraction modelTransform interaction =
            match interaction with
            | Drag movement ->
                match movement.Type with
                | Rotate ->
                    let dx = DragMovement.dx movement
                    let dy = DragMovement.dy movement
                    let aboutY = dx * System.MathF.PI
                    let aboutZ = dy * System.MathF.PI
                    let rotation =
                        Matrix4x4.CreateTranslation(-modelTransform.Center) *
                        Matrix4x4.CreateFromYawPitchRoll(aboutY, aboutZ, 0f) *
                        Matrix4x4.CreateTranslation(modelTransform.Center)

                    { modelTransform with Rotation = modelTransform.Rotation * rotation }

    type DragState =
        | NotDragging
        | Dragging of DragMovement

    type Transform =
        { DragState : DragState; StableTransform : ModelTransform; CurrentTransform : ModelTransform}

    module Transform =
        let initial = { DragState = NotDragging; StableTransform = ModelTransform.defaults; CurrentTransform = ModelTransform.defaults }
        let effective t = t.CurrentTransform |> ModelTransform.matrix
        let commit interaction t =
            let final = ModelTransform.applyInteraction t.StableTransform interaction
            { DragState = NotDragging; StableTransform = final; CurrentTransform = final }
        let previewDrag dragMovement t =
            let current = ModelTransform.applyInteraction t.StableTransform (Drag dragMovement)
            { t with DragState = Dragging dragMovement; CurrentTransform = current }

    let mouseDown transform (size, point) =
        match transform.DragState with
        | NotDragging ->
            let movement = { ControlSize = size; From = point; To = point; Type = Rotate }
            transform |> Transform.previewDrag movement
        | Dragging prev ->
            let movement = { prev with To = point }
            transform |> Transform.previewDrag movement

    let mouseMove transform (_, point) =
        match transform.DragState with
        | NotDragging ->
            transform
        | Dragging prev ->
            let movement = { prev with To = point }
            transform |> Transform.previewDrag movement

    let mouseUp transform (_, point) =
        match transform.DragState with
        | NotDragging ->
            transform
        | Dragging prev ->
            let movement = { prev with To = point }
            transform |> Transform.commit (Drag movement)
