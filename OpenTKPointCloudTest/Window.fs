namespace OpenTKPointCloudTest

open OpenTK.Windowing.Desktop
open OpenTK.Mathematics
open OpenTK.Graphics.OpenGL4
open OpenTK.Windowing.Common
open OpenTK.Windowing.Desktop
open OpenTK.Windowing.GraphicsLibraryFramework

type Window() =
    inherit GameWindow(GameWindowSettings.Default, NativeWindowSettings(Size = Vector2i(1024, 768), Title = "OpenTK Point Cloud Test"))

    let viewModel : PointCloudViewModel =
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

        printfn "Creating PointCloudViewModel"
        { Cloud1 = generateCloud 6325 -1f; Cloud2 = generateCloud 5478 1f }

    let mutable glHandle : GL option = None
    let mutable glState : GLState option = None
    let mutable nDraw = 0
    let mutable width = 1024.0
    let mutable height = 768.0
    let mutable mouseWasDown = false

    static let cameraPos = Vector3(0f, 0f, 3.0f)
    static let cameraFront = Vector3(0f, 0f, -1f)
    static let cameraUp = Vector3(0f, 1f, 0f)

    static let vertexShaderSource = """#version 330 core
        layout (location = 0) in vec3 aPos;
        layout (location = 1) in float aIntensity;

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

    static let fragmentShaderSource = """#version 330 core
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
        GL.BindVertexArray vertexArray
        let vboCube = OpenGLHelpers.copyItemsToBuffer cubeVertices
        let iboCube = OpenGLHelpers.copyIndexesToBuffer cubeIndices

        let positionLocation = 0u
        GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, sizeof<Vector3>, nativeint 0)

        GL.BindVertexArray 0u

        { BuffersForDraw = { Vertexes = vboCube; VertexArray = vertexArray; PrimitiveCount = 36; PrimitiveType = PrimitiveType.Triangles }; Indexes = iboCube; IndexType = DrawElementsType.UnsignedInt }

    member this.UpdateGlState (newState : GLState) =
        glState <- Some newState

    override this.OnLoad () =
        base.OnLoad()

        let cloud1VertexArray = GL.GenVertexArray()
        let cloud2VertexArray = GL.GenVertexArray()

        let positionLocation = 0
        let intensityLocation = 1

        let cloud1VertexBuffer =
            GL.BindVertexArray cloud1VertexArray

            let vertexBufferObject1 = OpenGLHelpers.copyItemsToBuffer viewModel.Cloud1
            GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, PointVertex.Size, PointVertex.PositionOffset)
            GL.VertexAttribPointer(intensityLocation, 1, VertexAttribPointerType.Float, false, PointVertex.Size, PointVertex.IntensityOffset)
            GL.EnableVertexAttribArray positionLocation
            GL.EnableVertexAttribArray intensityLocation

            GL.BindVertexArray 0u

            { Vertexes = vertexBufferObject1; VertexArray = cloud1VertexArray; PrimitiveType = PrimitiveType.Points; PrimitiveCount = viewModel.Cloud1.Length }

        let cloud2VertexBuffer =
            GL.BindVertexArray cloud2VertexArray

            let vertexBufferObject2 = OpenGLHelpers.copyItemsToBuffer viewModel.Cloud2
            GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, PointVertex.Size, PointVertex.PositionOffset)
            GL.VertexAttribPointer(intensityLocation, 1, VertexAttribPointerType.Float, false, PointVertex.Size, PointVertex.IntensityOffset)
            GL.EnableVertexAttribArray positionLocation
            GL.EnableVertexAttribArray intensityLocation

            GL.BindVertexArray 0u

            { Vertexes = vertexBufferObject2; VertexArray = cloud2VertexArray; PrimitiveType = PrimitiveType.Points; PrimitiveCount = viewModel.Cloud2.Length }

        let vertexShader = GL.CreateShader ShaderType.VertexShader
        OpenGLHelpers.compileShaderSource vertexShader vertexShaderSource

        let fragmentShader = GL.CreateShader ShaderType.FragmentShader
        OpenGLHelpers.compileShaderSource fragmentShader fragmentShaderSource

        let shaderProgram = GL.CreateProgram()
        GL.AttachShader(shaderProgram, vertexShader)
        GL.AttachShader(shaderProgram, fragmentShader)
        GL.BindAttribLocation(shaderProgram, positionLocation, "aPos")
        GL.BindAttribLocation(shaderProgram, intensityLocation, "aIntensity")
        GL.LinkProgram shaderProgram

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

    override _.OnRenderFrame(e:FrameEventArgs) =
        printfn $"OnOpenGlRender {System.Threading.Interlocked.Increment &nDraw}"

        base.OnRenderFrame(e)

        GL.Clear(ClearBufferMask.ColorBufferBit)

        match glState with
        | None ->
            printfn "GL Data not created"
        | Some glState ->
            GL.UseProgram(glState.ShaderProgram)

            let model = glState.ModelTransform.CurrentTransform |> MouseInteraction.ModelTransform.matrix
            let pointCloud1 = glState.Cloud1Buffer
            let pointCloud2 = glState.Cloud2Buffer

            GL.Enable EnableCap.CullFace
            GL.FrontFace FrontFaceDirection.Cw
            GL.CullFace CullFaceMode.Back
            GL.Enable EnableCap.DepthTest

            GL.ClearColor(0f, 0f, 0f, 1f)
            GL.Clear (ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)
            GL.Viewport (System.Drawing.Rectangle(0, 0, int width, int height))

            GL.UseProgram glState.ShaderProgram

            let modelLoc = GL.GetUniformLocation (glState.ShaderProgram, "uModel")
            let viewLoc = GL.GetUniformLocation (glState.ShaderProgram, "uView")
            let projectionLoc = GL.GetUniformLocation (glState.ShaderProgram, "uProjection")

            let view = Matrix4.LookAt(cameraPos, cameraPos+cameraFront, cameraUp)
            let projection = Matrix4.CreatePerspectiveFieldOfView(single (System.Math.PI / 2.), single (width / height), 0.1f, 100.0f)

            OpenGLHelpers.uniformMatrix4fv viewLoc false &view
            OpenGLHelpers.uniformMatrix4fv modelLoc false &model
            OpenGLHelpers.uniformMatrix4fv projectionLoc false &projection

            // Cloud
            GL.BindVertexArray pointCloud1.VertexArray
            GL.EnableVertexAttribArray glState.PositionLocation
            GL.EnableVertexAttribArray glState.IntensityLocation
            GL.DrawArrays (pointCloud1.PrimitiveType, 0, pointCloud1.PrimitiveCount)
            GL.DisableVertexAttribArray glState.PositionLocation
            GL.DisableVertexAttribArray glState.IntensityLocation

            GL.BindVertexArray pointCloud2.VertexArray
            GL.EnableVertexAttribArray glState.PositionLocation
            GL.EnableVertexAttribArray glState.IntensityLocation
            GL.DrawArrays (pointCloud2.PrimitiveType, 0, pointCloud2.PrimitiveCount)
            GL.DisableVertexAttribArray glState.PositionLocation
            GL.DisableVertexAttribArray glState.IntensityLocation

            // Cube
            GL.PolygonMode (MaterialFace.FrontAndBack, PolygonMode.Line)
            GL.BindVertexArray glState.ContainingCube.BuffersForDraw.VertexArray
            GL.EnableVertexAttribArray glState.PositionLocation
            GL.DrawElements (glState.ContainingCube.BuffersForDraw.PrimitiveType, glState.ContainingCube.BuffersForDraw.PrimitiveCount, glState.ContainingCube.IndexType, nativeint 0)
            GL.DisableVertexAttribArray glState.PositionLocation

            GL.BindVertexArray 0u

        base.SwapBuffers()

    override _.OnResize(e:ResizeEventArgs) =
        base.OnResize(e)
        width <- e.Width
        height <- e.Height
        GL.Viewport(0, 0, e.Width, e.Height);

    override this.OnUpdateFrame(e:FrameEventArgs) =
        base.OnUpdateFrame(e)

        let mouseState = this.MouseState

        match glState with
        | None -> ()
        | Some glState ->
            let size = Vector2(single width, single height)
            let point = mouseState.Position
            let mouseIsDown = mouseState.IsButtonDown(MouseButton.Left)

            match mouseWasDown, mouseIsDown with
            | (false, true) ->
                let transform = MouseInteraction.mouseDown glState.ModelTransform (size, point)
                this.UpdateGlState { glState with ModelTransform = transform }
            | (true, false) ->
                let transform = MouseInteraction.mouseUp glState.ModelTransform (size, point)
                this.UpdateGlState { glState with ModelTransform = transform }
            | (true, true)
            | (false, false) ->
                let transform = MouseInteraction.mouseMove glState.ModelTransform (size, point)
                this.UpdateGlState { glState with ModelTransform = transform }

            mouseWasDown <- mouseIsDown