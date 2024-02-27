namespace PointCloudTest

open System
open System.Numerics
open Silk.NET.OpenGL
open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open System.Text

#nowarn "9"
#nowarn "51"
module VoidPtr =
    let ofInt i = nativeint i |> NativeInterop.NativePtr.ofNativeInt<byte> |> NativePtr.toVoidPtr
    let zero = ofInt 0

module OpenGLHelpers =
    let checkGL (gl : GL) =
        let err = gl.GetError()
        if err <> GLEnum.NoError then
            failwithf "GL Error %A" err

    let copyItemsToBuffer (gl : GL) (points: 'vertex[]) =
        let vertexBufferObject = gl.GenBuffer()
        checkGL gl

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBufferObject)
        checkGL gl

        let size = unativeint (uint points.Length * uint sizeof<'vertex>)
        gl.BufferData(BufferTargetARB.ArrayBuffer, size, ReadOnlySpan points, BufferUsageARB.StaticDraw)
        checkGL gl

        Buffer vertexBufferObject

    let copyIndexesToBuffer (gl : GL) (indexes: uint[]) =
        let vertexBufferObject = gl.GenBuffer()
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, vertexBufferObject)
        checkGL gl

        let size = unativeint (indexes.Length * sizeof<uint>)
        gl.BufferData(BufferTargetARB.ElementArrayBuffer, size, ReadOnlySpan indexes, BufferUsageARB.StaticDraw)
        checkGL gl

        Buffer vertexBufferObject

    let compileShaderSource (gl : GL) shader source =
        gl.ShaderSource(shader, source)
        checkGL gl

        gl.CompileShader(shader)
        checkGL gl

        let log = gl.GetShaderInfoLog(shader)
        printfn "Shader compilation log:"
        printfn "%s" log

        let status = gl.GetShader(shader, ShaderParameterName.CompileStatus)
        if status = 0 then
            printfn "Shader compilation failed"

    let uniformMatrix4fv (gl : GL) (uniformLocation : int) (transpose : bool) (model : inref<Matrix4x4>) =
        let model = NativePtr.toVoidPtr &&model
        gl.UniformMatrix4(uniformLocation, 1u, transpose, NativePtr.ofVoidPtr<float32> model)

    let dproc (source : GLEnum) (type_ : GLEnum) (id : int) (severity : GLEnum) (length : int) (message : nativeint) (userParam : nativeint) =
        let bytes = Array.zeroCreate<byte> length
        Marshal.Copy(message, bytes, 0, length)
        let message = Encoding.ASCII.GetString(bytes)
        printfn "GL Debug: %A %A %A %A %A %A" source type_ id severity length message

    let enableDebugging (gl : GL) =
        gl.Enable EnableCap.DebugOutput
        checkGL gl

        try
            gl.DebugMessageCallback(dproc, VoidPtr.zero)
            checkGL gl
        with
        | :? Silk.NET.Core.Loader.SymbolLoadingException as e ->
            printfn "Error: %A" e

