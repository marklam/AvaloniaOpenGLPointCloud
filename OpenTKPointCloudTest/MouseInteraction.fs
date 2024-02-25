namespace OpenTKPointCloudTest

open OpenTK.Mathematics
open System

module MouseInteraction =
    type DragType = | Rotate

    type DragMovement =
        {
            ControlSize : Vector2
            From        : Vector2
            To          : Vector2
            Type        : DragType
        }

    module DragMovement =
        let dx (dm : DragMovement) = (dm.To.X - dm.From.X) / dm.ControlSize.X |> single
        let dy (dm : DragMovement) = (dm.To.Y - dm.From.Y) / dm.ControlSize.Y |> single

    type TransformInteraction =
    | Drag of DragMovement

    type ModelTransform =
        {
            Center         : Vector3
            Translation    : Matrix4
            Rotation       : Matrix4
            Scaling        : Matrix4
            PanSensitivity : single
        }

    module ModelTransform =
        let matrix modelTransform =
            modelTransform.Scaling * modelTransform.Rotation * modelTransform.Translation

        let normMatrix modelTransform =
            try
                let inv = Matrix4.Invert(modelTransform.Rotation)
                Matrix4.Transpose(inv)
            with
            | :? InvalidOperationException ->
                Matrix4.Identity

        let defaults =
            {
                Center         = Vector3.Zero
                Translation    = Matrix4.Identity
                Rotation       = Matrix4.Identity
                Scaling        = Matrix4.Identity
                PanSensitivity = 2.0f
            }

        let createFromYawPitchRoll (y, p, r) =
            let m = System.Numerics.Matrix4x4.CreateFromYawPitchRoll(y, p, r)
            Matrix4(
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44
            )

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
                        Matrix4.CreateTranslation(-modelTransform.Center) *
                        createFromYawPitchRoll(aboutY, aboutZ, 0f) *
                        Matrix4.CreateTranslation(modelTransform.Center)

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
