namespace OpenTKPointCloudTest

open System
open System.Runtime.InteropServices
open OpenTK.Graphics.OpenGL
open OpenTK.Mathematics
open Microsoft.FSharp.NativeInterop

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

#nowarn "9"
#nowarn "51"
module OpenGLHelpers =
    let copyItemsToBuffer (points: 'vertex[]) =
        let vertexBufferObject = GL.GenBuffer()
        GL.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBufferObject)
        GL.BufferData(BufferTargetARB.ArrayBuffer, ReadOnlySpan points, BufferUsageARB.StaticDraw)
        vertexBufferObject

    let copyIndexesToBuffer (indexes: uint[]) =
        let vertexBufferObject = GL.GenBuffer()
        GL.BindBuffer(BufferTargetARB.ElementArrayBuffer, vertexBufferObject)
        GL.BufferData(BufferTargetARB.ElementArrayBuffer, ReadOnlySpan indexes, BufferUsageARB.StaticDraw)
        vertexBufferObject

    let compileShaderSource shader source =
        GL.ShaderSource(shader, source)
        GL.CompileShader(shader)

    let uniformMatrix4fv (program:int) (uniformLocation : int) (transpose : bool) (model : inref<Matrix4>) =
        GL.ProgramUniformMatrix4f(program, uniformLocation, 1, transpose, &model)