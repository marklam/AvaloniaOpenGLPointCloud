namespace PointCloudTest

open System.Runtime.InteropServices
open OpenTK
open OpenTK.Graphics.OpenGL
open OpenTK.Mathematics
open Avalonia
open Avalonia.Threading
open Avalonia.OpenGL
open Avalonia.OpenGL.Controls
open Avalonia.Input
open Avalonia.Rendering
open OpenTK.Graphics

#nowarn "9"
module VoidPtr =
    open Microsoft.FSharp.NativeInterop
    let ofInt i = nativeint i |> NativeInterop.NativePtr.ofNativeInt<byte> |> NativePtr.toVoidPtr
    let zero = ofInt 0

type BuffersForDraw =
    {
        Vertexes       : int
        VertexArray    : int
        PrimitiveCount : int
        PrimitiveType  : PrimitiveType
    }

type BuffersForIndexedDraw =
    {
        BuffersForDraw : BuffersForDraw
        Indexes        : int
        IndexType      : DrawElementsType
    }

type GLState =
    {
        Cloud1Buffer      : BuffersForDraw
        Cloud2Buffer      : BuffersForDraw
        ContainingCube    : BuffersForIndexedDraw
        PositionLocation  : uint
        IntensityLocation : uint
        ShaderProgram     : int
        ModelTransform    : MouseInteraction.Transform
    }

[<Struct; StructLayout(LayoutKind.Sequential, Pack = 4)>]
type PointVertex  = { Position : Vector3; Intensity : single } with
    static member PositionOffset  = nativeint 0
    static member IntensityOffset = nativeint sizeof<Vector3>
    static member Size            = sizeof<PointVertex>

type PointCloudViewModel = { Cloud1 : PointVertex[]; Cloud2 : PointVertex[] }

type PointCloudControl() =
    inherit OpenGlControlBase()

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

    let containingCube () =
        let vertexArray = GL.GenVertexArray()
        OpenGLHelpers.checkGL()

        GL.BindVertexArray vertexArray
        OpenGLHelpers.checkGL()

        let vboCube = OpenGLHelpers.copyItemsToBuffer cubeVertices
        let iboCube = OpenGLHelpers.copyIndexesToBuffer cubeIndices

        let positionLocation = 0u
        GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, sizeof<Vector3>, nativeint 0)
        OpenGLHelpers.checkGL()

        GL.BindVertexArray 0
        OpenGLHelpers.checkGL()

        { BuffersForDraw = { Vertexes = vboCube; VertexArray = vertexArray; PrimitiveCount = 36; PrimitiveType = PrimitiveType.Triangles }; Indexes = iboCube; IndexType = DrawElementsType.UnsignedInt }

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

    member this.GlInit () =
        match this.ViewModel with
        | None ->
            printfn "Invalid DataContext"
        | Some (state : PointCloudViewModel) ->
            GL.GetError() |> ignore
            OpenGLHelpers.enableDebugging()

            let cloud1VertexArray = GL.GenVertexArray()
            OpenGLHelpers.checkGL()

            let cloud2VertexArray = GL.GenVertexArray()
            OpenGLHelpers.checkGL()

            let positionLocation = 0u
            let intensityLocation = 1u

            let cloud1VertexBuffer =
                GL.BindVertexArray cloud1VertexArray
                OpenGLHelpers.checkGL()

                let vertexBufferObject1 = OpenGLHelpers.copyItemsToBuffer state.Cloud1
                GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, PointVertex.Size, PointVertex.PositionOffset)
                OpenGLHelpers.checkGL()

                GL.VertexAttribPointer(intensityLocation, 1, VertexAttribPointerType.Float, false, PointVertex.Size, PointVertex.IntensityOffset)
                OpenGLHelpers.checkGL()

                GL.EnableVertexAttribArray positionLocation
                OpenGLHelpers.checkGL()

                GL.EnableVertexAttribArray intensityLocation
                OpenGLHelpers.checkGL()

                GL.BindVertexArray 0
                OpenGLHelpers.checkGL()

                { Vertexes = vertexBufferObject1; VertexArray = cloud1VertexArray; PrimitiveType = PrimitiveType.Points; PrimitiveCount = state.Cloud1.Length }

            let cloud2VertexBuffer =
                GL.BindVertexArray cloud2VertexArray
                OpenGLHelpers.checkGL()

                let vertexBufferObject2 = OpenGLHelpers.copyItemsToBuffer state.Cloud2
                GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, PointVertex.Size, PointVertex.PositionOffset)
                OpenGLHelpers.checkGL()

                GL.VertexAttribPointer(intensityLocation, 1, VertexAttribPointerType.Float, false, PointVertex.Size, PointVertex.IntensityOffset)
                OpenGLHelpers.checkGL()

                GL.EnableVertexAttribArray positionLocation
                OpenGLHelpers.checkGL()

                GL.EnableVertexAttribArray intensityLocation
                OpenGLHelpers.checkGL()

                GL.BindVertexArray 0
                OpenGLHelpers.checkGL()

                { Vertexes = vertexBufferObject2; VertexArray = cloud2VertexArray; PrimitiveType = PrimitiveType.Points; PrimitiveCount = state.Cloud2.Length }

            let vertexShader = GL.CreateShader ShaderType.VertexShader
            OpenGLHelpers.checkGL()

            OpenGLHelpers.compileShaderSource vertexShader vertexShaderSource

            let fragmentShader = GL.CreateShader ShaderType.FragmentShader
            OpenGLHelpers.checkGL()

            OpenGLHelpers.compileShaderSource fragmentShader fragmentShaderSource

            let shaderProgram = GL.CreateProgram()
            OpenGLHelpers.checkGL()

            GL.AttachShader(shaderProgram, vertexShader)
            OpenGLHelpers.checkGL()

            GL.AttachShader(shaderProgram, fragmentShader)
            OpenGLHelpers.checkGL()

            GL.BindAttribLocation(shaderProgram, positionLocation, "aPos")
            OpenGLHelpers.checkGL()

            GL.BindAttribLocation(shaderProgram, intensityLocation, "aIntensity")
            OpenGLHelpers.checkGL()

            GL.LinkProgram shaderProgram
            OpenGLHelpers.checkGL()

            let mutable message = GL.GetProgramInfoLog(shaderProgram)
            printfn "Shader program link message"
            printfn "%s" message

            GL.ValidateProgram shaderProgram
            OpenGLHelpers.checkGL()

            let cube = containingCube()

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

    member _.GlRender (bounds : Rect) =
        match glState with
        | None ->
            printfn "GL Data not created"
        | Some glState ->
            let model = glState.ModelTransform.CurrentTransform |> MouseInteraction.ModelTransform.matrix
            let pointCloud1 = glState.Cloud1Buffer
            let pointCloud2 = glState.Cloud2Buffer

            GL.Enable EnableCap.CullFace
            OpenGLHelpers.checkGL()

            GL.FrontFace FrontFaceDirection.Cw
            OpenGLHelpers.checkGL()

            GL.CullFace TriangleFace.Back
            OpenGLHelpers.checkGL()

            GL.Enable EnableCap.DepthTest
            OpenGLHelpers.checkGL()

            GL.ClearColor(0f, 0f, 0f, 1f)
            OpenGLHelpers.checkGL()

            GL.Clear (ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)
            OpenGLHelpers.checkGL()

            GL.Viewport (0, 0, (int)(bounds.Width * 1.5), (int)(bounds.Height * 1.5))
            OpenGLHelpers.checkGL()

            GL.UseProgram glState.ShaderProgram
            OpenGLHelpers.checkGL()

            let modelLoc = GL.GetUniformLocation (glState.ShaderProgram, "uModel")
            OpenGLHelpers.checkGL()

            let viewLoc = GL.GetUniformLocation (glState.ShaderProgram, "uView")
            OpenGLHelpers.checkGL()

            let projectionLoc = GL.GetUniformLocation (glState.ShaderProgram, "uProjection")
            OpenGLHelpers.checkGL()

            let view = Matrix4.LookAt(cameraPos, cameraPos+cameraFront, cameraUp)
            let projection = Matrix4.CreatePerspectiveFieldOfView(single (System.Math.PI / 2.), single (bounds.Width / bounds.Height), 0.1f, 100.0f)

            OpenGLHelpers.uniformMatrix4fv viewLoc false &view
            OpenGLHelpers.uniformMatrix4fv modelLoc false &model
            OpenGLHelpers.uniformMatrix4fv projectionLoc false &projection

            // Cloud

            GL.BindVertexArray pointCloud1.VertexArray
            OpenGLHelpers.checkGL()

            GL.EnableVertexAttribArray glState.PositionLocation
            OpenGLHelpers.checkGL()

            GL.EnableVertexAttribArray glState.IntensityLocation
            OpenGLHelpers.checkGL()

            GL.DrawArrays (pointCloud1.PrimitiveType, 0, pointCloud1.PrimitiveCount)
            OpenGLHelpers.checkGL()

            GL.DisableVertexAttribArray glState.PositionLocation
            OpenGLHelpers.checkGL()

            GL.DisableVertexAttribArray glState.IntensityLocation
            OpenGLHelpers.checkGL()

            GL.BindVertexArray pointCloud2.VertexArray
            OpenGLHelpers.checkGL()

            GL.EnableVertexAttribArray glState.PositionLocation
            OpenGLHelpers.checkGL()

            GL.EnableVertexAttribArray glState.IntensityLocation
            OpenGLHelpers.checkGL()

            GL.DrawArrays (pointCloud2.PrimitiveType, 0, pointCloud2.PrimitiveCount)
            OpenGLHelpers.checkGL()

            GL.DisableVertexAttribArray glState.PositionLocation
            OpenGLHelpers.checkGL()

            GL.DisableVertexAttribArray glState.IntensityLocation
            OpenGLHelpers.checkGL()

            // Cube
            try
                GL.PolygonMode (TriangleFace.FrontAndBack, PolygonMode.Line)
                OpenGLHelpers.checkGL()

                GL.BindVertexArray glState.ContainingCube.BuffersForDraw.VertexArray
                OpenGLHelpers.checkGL()

                GL.EnableVertexAttribArray glState.PositionLocation
                OpenGLHelpers.checkGL()

                GL.DrawElements (glState.ContainingCube.BuffersForDraw.PrimitiveType, glState.ContainingCube.BuffersForDraw.PrimitiveCount, glState.ContainingCube.IndexType, nativeint 0)
                OpenGLHelpers.checkGL()

                GL.DisableVertexAttribArray glState.PositionLocation
                OpenGLHelpers.checkGL()

                GL.BindVertexArray 0
                OpenGLHelpers.checkGL()
            with // Can't actually catch this, but it's because the glPolygonMode call is not supported in the Angle DLL
            | :? System.AccessViolationException as e ->
                printfn "AccessViolationException: %A" e

    member _.GlDeInit () =
        // Unbind everything
        GL.BindBuffer (BufferTargetARB.ArrayBuffer, 0)
        GL.BindBuffer (BufferTargetARB.ElementArrayBuffer, 0)
        GL.BindVertexArray 0
        GL.UseProgram 0

        match glState with
        | None -> ()
        | Some glState ->
            // Delete all resources.
            // TODO - delete buffers
            GL.DeleteProgram glState.ShaderProgram

    member this.Cleanup() =
        this.GlDeInit ()
        glState <- None

    member this.Setup(gl : GlInterface) =
        GLLoader.LoadBindings { new IBindingsContext with member _.GetProcAddress x = gl.GetProcAddress x }
        GL.GetError() |> ignore

        this.GlInit ()

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
        this.GlRender this.Bounds

    override _.EndInit() =
        base.RequestNextFrameRendering()

    member this.ProcessMouseArgs (p : PointerEventArgs) =
        let point = p.GetCurrentPoint(this).Position
        let size = this.Bounds.Size
        (Vector2(single size.Width, single size.Height), Vector2(single point.X, single point.Y))

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
