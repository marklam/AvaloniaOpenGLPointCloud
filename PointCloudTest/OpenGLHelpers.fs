namespace PointCloudTest

open System
open OpenTK.Graphics.OpenGL4
open OpenTK.Mathematics
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"
module OpenGLHelpers =
    let copyItemsToBuffer (points: 'vertex[]) =
        let vertexBufferObject = GL.GenBuffer()
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferObject)
        let size = points.Length * sizeof<'vertex>
        GL.BufferData(BufferTarget.ArrayBuffer, size, points, BufferUsageHint.StaticDraw)
        vertexBufferObject

    let copyIndexesToBuffer (indexes: uint[]) =
        let vertexBufferObject = GL.GenBuffer()
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, vertexBufferObject)
        let size = indexes.Length * sizeof<uint>
        GL.BufferData(BufferTarget.ElementArrayBuffer, size, indexes, BufferUsageHint.StaticDraw)
        vertexBufferObject

    let compileShaderSource shader source =
        GL.ShaderSource(shader, source)
        GL.CompileShader(shader)

    let uniformMatrix4fv (uniformLocation : int) (transpose : bool) (model : inref<Matrix4>) =
        let model = &&model |> NativePtr.toVoidPtr |> NativePtr.ofVoidPtr<float32>
        GL.UniformMatrix4(uniformLocation, 1, transpose, model)