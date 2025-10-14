using Microsoft.Xna.Framework.Graphics;

namespace Cataphract.Core;

internal interface IShaderParameters
{
    void Apply(EffectParameterCollection parameters);
}