namespace PointCloudTest

open System.Runtime.InteropServices
open System.Numerics
open Silk.NET.OpenGL
open Avalonia
open Avalonia.Threading
open Avalonia.OpenGL
open Avalonia.OpenGL.Controls
open Avalonia.Input
open Avalonia.Rendering

type BuffersForDraw =
    {
        Vertexes       : Buffer
        VertexArray    : VertexArray
        PrimitiveCount : uint
        PrimitiveType  : PrimitiveType
    }

type BuffersForIndexedDraw =
    {
        BuffersForDraw : BuffersForDraw
        Indexes        : Buffer
        IndexType      : DrawElementsType
    }

type GLState =
    {
        Cloud1Buffer      : BuffersForDraw
        Cloud2Buffer      : BuffersForDraw
        ContainingCube    : BuffersForIndexedDraw
        PositionLocation  : uint
        IntensityLocation : uint
        ShaderProgram     : uint
        ModelTransform    : MouseInteraction.Transform
    }

[<Struct; StructLayout(LayoutKind.Sequential, Pack = 4)>]
type PointVertex  = { Position : Vector3; Intensity : single } with
    static member PositionOffset  = VoidPtr.zero
    static member IntensityOffset = VoidPtr.ofInt sizeof<Vector3>
    static member Size            = uint32 sizeof<PointVertex>

type PointCloudViewModel = { Cloud1 : PointVertex[]; Cloud2 : PointVertex[] }

type PointCloudControl() =
    inherit OpenGlControlBase()

    let mutable glHandle : GL option = None
    let mutable glState : GLState option = None
    let mutable nDraw = 0

    static let cameraPos = Vector3(0f, 0f, 3.0f)
    static let cameraFront = Vector3(0f, 0f, -1f)
    static let cameraUp = Vector3(0f, 1f, 0f)

    static let vertexShaderSource = """#version 300 es
        in vec3 aPos;
        in float aIntensity;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;

        out vec4 Color;

        void main()
        {
            gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0);
            //Color = vec4(aPos.z/2+0.5, aPos.x/2+0.5, aPos.y/2+0.5, 1.0);
            Color = vec4(normalize(vec3(1.0, aIntensity, 0.0)),1.0);
        }
        """

    static let fragmentShaderSource = """#version 300 es
        precision mediump float;
        in vec4 Color;
        out vec4 FragColor;

        void main()
        {
            FragColor = Color;
        }
        """

    static let cubeVertices = [|
        Vector3( 1.0f,  1.0f,  1.0f)
        Vector3(-1.0f,  1.0f, -1.0f)
        Vector3(-1.0f,  1.0f,  1.0f)
        Vector3( 1.0f, -1.0f, -1.0f)
        Vector3(-1.0f, -1.0f, -1.0f)
        Vector3( 1.0f,  1.0f, -1.0f)
        Vector3( 1.0f, -1.0f,  1.0f)
        Vector3(-1.0f, -1.0f,  1.0f)
    |]

    static let cubeIndices = [|
        0u; 1u; 2u
        1u; 3u; 4u
        5u; 6u; 3u
        7u; 3u; 6u
        2u; 4u; 7u
        0u; 7u; 6u
        0u; 5u; 1u
        1u; 5u; 3u
        5u; 0u; 6u
        7u; 4u; 3u
        2u; 1u; 4u
        0u; 2u; 7u
    |]

    let containingCube (gl:GL) =
        let vertexArray = gl.GenVertexArray()
        OpenGLHelpers.checkGL gl
        gl.BindVertexArray vertexArray
        OpenGLHelpers.checkGL gl
        let vboCube = OpenGLHelpers.copyItemsToBuffer gl cubeVertices
        let iboCube = OpenGLHelpers.copyIndexesToBuffer gl cubeIndices

        let positionLocation = 0u
        gl.VertexAttribPointer(positionLocation, 3, GLEnum.Float, false, (uint sizeof<Vector3>), VoidPtr.zero)
        OpenGLHelpers.checkGL gl

        gl.BindVertexArray 0u
        OpenGLHelpers.checkGL gl

        { BuffersForDraw = { Vertexes = vboCube; VertexArray = VertexArray vertexArray; PrimitiveCount = 36u; PrimitiveType = PrimitiveType.Triangles }; Indexes = iboCube; IndexType = DrawElementsType.UnsignedInt }

    member this.ViewModel =
        match this.DataContext with
        | :? PointCloudViewModel as state ->
            Some state
        | _ ->
            None

    member this.UpdateGlState state =
        let newState = Some state
        let changed = glState <> newState
        glState <- newState
        if changed then
            this.RequestNextFrameRendering()

    member this.GlInit (gl:GL) =
        match this.ViewModel with
        | None ->
            printfn "Invalid DataContext"
        | Some (state : PointCloudViewModel) ->
            gl.GetError() |> ignore

            OpenGLHelpers.enableDebugging gl
            OpenGLHelpers.checkGL gl

            let cloud1VertexArray = gl.GenVertexArray()
            OpenGLHelpers.checkGL gl

            let cloud2VertexArray = gl.GenVertexArray()
            OpenGLHelpers.checkGL gl

            let positionLocation = 0u
            let intensityLocation = 1u

            let cloud1VertexBuffer =
                gl.BindVertexArray cloud1VertexArray
                OpenGLHelpers.checkGL gl

                let vertexBufferObject1 = OpenGLHelpers.copyItemsToBuffer gl state.Cloud1
                gl.VertexAttribPointer(positionLocation, 3, GLEnum.Float, false, PointVertex.Size, PointVertex.PositionOffset)
                OpenGLHelpers.checkGL gl

                gl.VertexAttribPointer(intensityLocation, 1, GLEnum.Float, false, PointVertex.Size, PointVertex.IntensityOffset)
                OpenGLHelpers.checkGL gl

                gl.EnableVertexAttribArray positionLocation
                OpenGLHelpers.checkGL gl

                gl.EnableVertexAttribArray intensityLocation
                OpenGLHelpers.checkGL gl

                gl.BindVertexArray 0u
                OpenGLHelpers.checkGL gl

                { Vertexes = vertexBufferObject1; VertexArray = VertexArray cloud1VertexArray; PrimitiveType = PrimitiveType.Points; PrimitiveCount = uint state.Cloud1.Length }

            let cloud2VertexBuffer =
                gl.BindVertexArray cloud2VertexArray
                OpenGLHelpers.checkGL gl

                let vertexBufferObject2 = OpenGLHelpers.copyItemsToBuffer gl state.Cloud2
                gl.VertexAttribPointer(positionLocation, 3, GLEnum.Float, false, PointVertex.Size, PointVertex.PositionOffset)
                OpenGLHelpers.checkGL gl

                gl.VertexAttribPointer(intensityLocation, 1, GLEnum.Float, false, PointVertex.Size, PointVertex.IntensityOffset)
                OpenGLHelpers.checkGL gl

                gl.EnableVertexAttribArray positionLocation
                OpenGLHelpers.checkGL gl

                gl.EnableVertexAttribArray intensityLocation
                OpenGLHelpers.checkGL gl

                gl.BindVertexArray 0u
                OpenGLHelpers.checkGL gl

                { Vertexes = vertexBufferObject2; VertexArray = VertexArray cloud2VertexArray; PrimitiveType = PrimitiveType.Points; PrimitiveCount = uint state.Cloud2.Length }

            let vertexShader = gl.CreateShader ShaderType.VertexShader
            OpenGLHelpers.checkGL gl

            OpenGLHelpers.compileShaderSource gl vertexShader vertexShaderSource

            let fragmentShader = gl.CreateShader ShaderType.FragmentShader
            OpenGLHelpers.checkGL gl

            OpenGLHelpers.compileShaderSource gl fragmentShader fragmentShaderSource

            let shaderProgram = gl.CreateProgram()
            OpenGLHelpers.checkGL gl

            gl.AttachShader(shaderProgram, vertexShader)
            OpenGLHelpers.checkGL gl

            gl.AttachShader(shaderProgram, fragmentShader)
            OpenGLHelpers.checkGL gl

            gl.BindAttribLocation(shaderProgram, positionLocation, "aPos")
            OpenGLHelpers.checkGL gl

            gl.BindAttribLocation(shaderProgram, intensityLocation, "aIntensity")
            OpenGLHelpers.checkGL gl

            gl.LinkProgram shaderProgram
            OpenGLHelpers.checkGL gl

            let mutable message = gl.GetProgramInfoLog(shaderProgram)
            printfn "Shader program link message"
            printfn "%s" message

            gl.ValidateProgram shaderProgram
            OpenGLHelpers.checkGL gl

            let cube = containingCube gl

            {
                Cloud1Buffer      = cloud1VertexBuffer
                Cloud2Buffer      = cloud2VertexBuffer
                PositionLocation  = positionLocation
                IntensityLocation = intensityLocation
                ShaderProgram     = shaderProgram
                ContainingCube    = cube
                ModelTransform    = MouseInteraction.Transform.initial
            }
            |> this.UpdateGlState

    member _.GlRender (gl : GL) (bounds : Rect) =
        match glState with
        | None ->
            printfn "GL Data not created"
        | Some glState ->
            let model = glState.ModelTransform.CurrentTransform |> MouseInteraction.ModelTransform.matrix
            let pointCloud1 = glState.Cloud1Buffer
            let pointCloud2 = glState.Cloud2Buffer

            gl.Enable EnableCap.CullFace
            OpenGLHelpers.checkGL gl
            gl.FrontFace FrontFaceDirection.CW
            OpenGLHelpers.checkGL gl
            gl.CullFace TriangleFace.Back
            OpenGLHelpers.checkGL gl
            gl.Enable EnableCap.DepthTest
            OpenGLHelpers.checkGL gl

            gl.ClearColor(0f, 0f, 0f, 1f)
            OpenGLHelpers.checkGL gl
            gl.Clear (ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)
            OpenGLHelpers.checkGL gl
            gl.Viewport (0, 0, (uint)(bounds.Width * 1.5), (uint)(bounds.Height * 1.5))
            OpenGLHelpers.checkGL gl

            gl.UseProgram glState.ShaderProgram
            OpenGLHelpers.checkGL gl

            let modelLoc = gl.GetUniformLocation (glState.ShaderProgram, "uModel")
            OpenGLHelpers.checkGL gl
            let viewLoc = gl.GetUniformLocation (glState.ShaderProgram, "uView")
            OpenGLHelpers.checkGL gl
            let projectionLoc = gl.GetUniformLocation (glState.ShaderProgram, "uProjection")
            OpenGLHelpers.checkGL gl

            let view = Matrix4x4.CreateLookAt(cameraPos, cameraPos+cameraFront, cameraUp)
            let projection = Matrix4x4.CreatePerspectiveFieldOfView(single (System.Math.PI / 2.), single (bounds.Width / bounds.Height), 0.1f, 100.0f)

            OpenGLHelpers.uniformMatrix4fv gl viewLoc false &view
            OpenGLHelpers.uniformMatrix4fv gl modelLoc false &model
            OpenGLHelpers.uniformMatrix4fv gl projectionLoc false &projection

            // Cloud
            gl.BindVertexArray pointCloud1.VertexArray.Handle
            OpenGLHelpers.checkGL gl

            gl.EnableVertexAttribArray glState.PositionLocation
            OpenGLHelpers.checkGL gl

            gl.EnableVertexAttribArray glState.IntensityLocation
            OpenGLHelpers.checkGL gl

            gl.DrawArrays (pointCloud1.PrimitiveType, 0, pointCloud1.PrimitiveCount)
            OpenGLHelpers.checkGL gl

            gl.DisableVertexAttribArray glState.PositionLocation
            OpenGLHelpers.checkGL gl

            gl.DisableVertexAttribArray glState.IntensityLocation
            OpenGLHelpers.checkGL gl

            gl.BindVertexArray pointCloud2.VertexArray.Handle
            OpenGLHelpers.checkGL gl

            gl.EnableVertexAttribArray glState.PositionLocation
            OpenGLHelpers.checkGL gl

            gl.EnableVertexAttribArray glState.IntensityLocation
            OpenGLHelpers.checkGL gl

            gl.DrawArrays (pointCloud2.PrimitiveType, 0, pointCloud2.PrimitiveCount)
            OpenGLHelpers.checkGL gl

            gl.DisableVertexAttribArray glState.PositionLocation
            OpenGLHelpers.checkGL gl

            gl.DisableVertexAttribArray glState.IntensityLocation
            OpenGLHelpers.checkGL gl

            // Cube
            try
                gl.PolygonMode (TriangleFace.FrontAndBack, PolygonMode.Line)
                OpenGLHelpers.checkGL gl

                gl.BindVertexArray glState.ContainingCube.BuffersForDraw.VertexArray.Handle
                OpenGLHelpers.checkGL gl

                gl.EnableVertexAttribArray glState.PositionLocation
                OpenGLHelpers.checkGL gl

                gl.DrawElements (glState.ContainingCube.BuffersForDraw.PrimitiveType, glState.ContainingCube.BuffersForDraw.PrimitiveCount, glState.ContainingCube.IndexType, VoidPtr.zero)
                OpenGLHelpers.checkGL gl

                gl.DisableVertexAttribArray glState.PositionLocation
                OpenGLHelpers.checkGL gl

                gl.BindVertexArray 0u
                OpenGLHelpers.checkGL gl
            with
            | :? Silk.NET.Core.Loader.SymbolLoadingException as e ->
                printfn "Error: %A" e

    member _.GlDeInit (gl : GL) =
        // Unbind everything
        gl.BindBuffer (BufferTargetARB.ArrayBuffer, 0u)
        gl.BindBuffer (BufferTargetARB.ElementArrayBuffer, 0u)
        gl.BindVertexArray 0u
        gl.UseProgram 0u

        match glState with
        | None -> ()
        | Some glState ->
            // Delete all resources.
            // TODO - delete buffers
            gl.DeleteProgram glState.ShaderProgram

    member this.Cleanup() =
        glHandle
        |> Option.iter (
            fun glapi ->
                let res = this.GlDeInit glapi
                glapi.Dispose()
                res
        )

        glHandle <- None
        glState <- None

    member this.Setup(gl : GlInterface) =
        let glapi = GL.GetApi gl.GetProcAddress
        this.GlInit glapi
        glHandle <- Some glapi

    override this.OnOpenGlInit(gl) =
        this.Setup(gl)
        Dispatcher.UIThread.Post(this.RequestNextFrameRendering, DispatcherPriority.ApplicationIdle)

    override this.OnOpenGlDeinit(gl) =
        this.Cleanup()

    override this.OnOpenGlLost() =
        printfn "OpenGL lost"
        this.Cleanup()
        base.OnOpenGlLost()

    override this.OnOpenGlRender(gl, fb) =
        printfn $"OnOpenGlRender {System.Threading.Interlocked.Increment &nDraw} fb={fb}"
        glHandle
        |> Option.iter (fun glapi -> this.GlRender glapi this.Bounds; glapi.Finish())

    override _.EndInit() =
        base.RequestNextFrameRendering()

    member this.ProcessMouseArgs (p : PointerEventArgs) =
        let point = p.GetCurrentPoint(this).Position
        let size = this.Bounds.Size
        (size, point)

    override this.OnPointerPressed (p : PointerPressedEventArgs) =
        match glState with
        | None -> ()
        | Some glState ->
            let (size, point) = this.ProcessMouseArgs p
            let transform = MouseInteraction.mouseDown glState.ModelTransform (size, point)
            this.UpdateGlState { glState with ModelTransform = transform }

    override this.OnPointerMoved (p : PointerEventArgs) =
        match glState with
        | None -> ()
        | Some glState ->
            let (size, point) = this.ProcessMouseArgs p
            let transform = MouseInteraction.mouseMove glState.ModelTransform (size, point)
            this.UpdateGlState { glState with ModelTransform = transform }

    override this.OnPointerReleased (p : PointerReleasedEventArgs) =
        match glState with
        | None -> ()
        | Some glState ->
            let (size, point) = this.ProcessMouseArgs p
            let transform = MouseInteraction.mouseUp glState.ModelTransform (size, point)
            this.UpdateGlState { glState with ModelTransform = transform }

    interface ICustomHitTest with
        member _.HitTest _ = true
