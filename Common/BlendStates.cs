using Microsoft.Xna.Framework.Graphics;

namespace Cataphract.Common;

public static class BlendStates
{
    // todo: convert to extension member once supported
    public static readonly BlendState Multiplicative = new BlendState
    {
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.Zero,
        ColorBlendFunction = BlendFunction.Add
    };
}