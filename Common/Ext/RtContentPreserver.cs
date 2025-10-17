
using Microsoft.Xna.Framework.Graphics;

using Terraria;
using Terraria.ModLoader;

namespace Cataphract.Common.Rendering;

internal sealed class RtContentPreserver : ILoadable
{
    void ILoadable.Load(Mod mod)
    {
        Main.RunOnMainThread(
            () =>
            {
                Main.graphics.GraphicsDevice.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;
                Main.graphics.ApplyChanges();
            }
        );
    }

    void ILoadable.Unload()
    {
    }

    public static void ApplyToBindings(RenderTargetBinding[] bindings)
    {
        foreach (var binding in bindings)
        {
            if (binding.RenderTarget is not RenderTarget2D rt)
            {
                continue;
            }

            rt.RenderTargetUsage = RenderTargetUsage.PreserveContents;
        }
    }
}