namespace PointCloudTest

open System
open System.Numerics
open Silk.NET.OpenGL
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"
module OpenGLHelpers =
    let copyItemsToBuffer (gl : GL) (points: 'vertex[]) =
        let vertexBufferObject = gl.GenBuffer()
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBufferObject)
        let size = unativeint (uint points.Length * uint sizeof<'vertex>)
        gl.BufferData(BufferTargetARB.ArrayBuffer, size, ReadOnlySpan points, BufferUsageARB.StaticDraw)
        Buffer vertexBufferObject

    let copyIndexesToBuffer (gl : GL) (indexes: uint[]) =
        let vertexBufferObject = gl.GenBuffer()
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, vertexBufferObject)
        let size = unativeint (indexes.Length * sizeof<uint>)
        gl.BufferData(BufferTargetARB.ElementArrayBuffer, size, ReadOnlySpan indexes, BufferUsageARB.StaticDraw)
        Buffer vertexBufferObject

    let compileShaderSource (gl : GL) shader source =
        gl.ShaderSource(shader, source)
        gl.CompileShader(shader)

    let uniformMatrix4fv (gl : GL) (uniformLocation : int) (transpose : bool) (model : inref<Matrix4x4>) =
        let model = NativePtr.toVoidPtr &&model
        gl.UniformMatrix4(uniformLocation, 1u, transpose, NativePtr.ofVoidPtr<float32> model)
