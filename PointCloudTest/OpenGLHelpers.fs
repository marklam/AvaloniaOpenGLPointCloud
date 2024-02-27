namespace PointCloudTest

open System
open System.Runtime.InteropServices
open System.Text
open OpenTK.Graphics.OpenGL
open OpenTK.Mathematics

module OpenGLHelpers =
    let checkGL () =
        let err = GL.GetError()
        if err <> ErrorCode.NoError then
            failwithf "GL Error %A" err

    let copyItemsToBuffer (points: 'vertex[]) =
        let vertexBufferObject = GL.GenBuffer()
        checkGL()

        GL.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBufferObject)
        checkGL()

        let size = points.Length * sizeof<'vertex>
        GL.BufferData(BufferTargetARB.ArrayBuffer, ReadOnlySpan points, BufferUsageARB.StaticDraw)
        checkGL()

        vertexBufferObject

    let copyIndexesToBuffer (indexes: uint[]) =
        let vertexBufferObject = GL.GenBuffer()
        GL.BindBuffer(BufferTargetARB.ElementArrayBuffer, vertexBufferObject)
        checkGL()

        GL.BufferData(BufferTargetARB.ElementArrayBuffer, ReadOnlySpan indexes, BufferUsageARB.StaticDraw)
        checkGL()

        vertexBufferObject

    let compileShaderSource shader source =
        GL.ShaderSource(shader, source)
        checkGL()

        GL.CompileShader(shader)
        checkGL()

        let log = GL.GetShaderInfoLog(shader)
        printfn "Shader compilation log:"
        printfn "%s" log

        //let status = GL.GetShader(shader, ShaderParameter.CompileStatus)
        //if status = 0 then
        //    printfn "Shader compilation failed"

    let uniformMatrix4fv (uniformLocation : int) (transpose : bool) (model : inref<Matrix4>) =
        GL.UniformMatrix4f(uniformLocation, 1, transpose, &model)
        checkGL()

    let dproc (source : DebugSource) (type_ : DebugType) (id : uint) (severity : DebugSeverity) (length : int) (message : nativeint) (userParam : nativeint) =
        let bytes = Array.zeroCreate<byte> length
        Marshal.Copy(message, bytes, 0, length)
        let message = Encoding.ASCII.GetString(bytes)
        printfn "GL Debug: %A %A %A %A %A %A" source type_ id severity length message

    let enableDebugging () =
        GL.Enable EnableCap.DebugOutput
        checkGL()

        GL.DebugMessageCallback(GLDebugProc dproc, nativeint 0)
        checkGL()
