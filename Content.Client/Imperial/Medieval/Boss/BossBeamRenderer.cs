using System.Numerics;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.Medieval.Boss;

public static class BossBeamRenderer
{
    public static void Draw(
        DrawingHandleWorld handle,
        Texture frame,
        BossBeamPlacement beam,
        BossBeamAnimationData animation,
        float referenceWidth,
        IReadOnlyList<Box2>? allowedAreas = null)
    {
        var geometry = BossBeamGeometry.Build(beam, frame.Size, animation, referenceWidth, allowedAreas); // clip this beam to its allowed areas
        if (geometry.Length == 0)
            return;

        var texture = frame;
        var offset = Vector2.Zero;
        while (texture is AtlasTexture atlas)                                    // find the full texture before mapping pixels
        {
            offset += atlas.SubRegion.TopLeft;
            texture = atlas.SourceTexture;
        }

        var vertices = new DrawVertexUV2D[geometry.Length];
        for (var index = 0; index < geometry.Length; index++)
        {
            var coordinate = AtlasCoordinate(geometry[index].TextureCoordinate, frame.Size, offset, texture.Size);
            vertices[index] = new(geometry[index].Position, coordinate);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, texture, vertices, Color.White);
    }

    public static Vector2 AtlasCoordinate(Vector2 frameCoordinate, Vector2i frameSize, Vector2 pixelOffset, Vector2i atlasSize)
    {
        var pixel = pixelOffset + frameCoordinate * frameSize;
        return new(pixel.X / atlasSize.X, 1 - pixel.Y / atlasSize.Y);
    }
}
