using System;
using System.Diagnostics;
using Cataphract.Common.Rendering;
using Cataphract.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Cataphract.Content.Items;

public class NeanderthalBlaster : ModItem
{
    public override string Texture => Assets.Images.Content.Items.Weapons.Misc.StupidGun.KEY;

    public override void SetDefaults()
    {
        Item.height = 24;
        Item.width = 64;
        Item.DamageType = DamageClass.Ranged;
        Item.damage = 14;
        Item.knockBack = 2f;
        Item.useTime = 60;
        Item.useAnimation = 60;
        Item.useStyle = -1;
        Item.noMelee = true;
        Item.value = Item.buyPrice(silver: 50);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = Assets.Audio.Misc.StupidGun_RegularFire.Asset with {
            Volume = 0.5f,
            PitchVariance = 0.2f
        };
        Item.shoot = ProjectileID.Bullet;
        Item.shootSpeed = 12f;
        Item.useAmmo = AmmoID.Bullet;

        base.SetDefaults();
    }

    public override bool CanShoot(Player player)
    {
        return base.CanShoot(player);
    }


    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        for (int i = 0; i < 15; i++)
        {
            NeanderthalBlasterSDFParticles.AddParticle(player.Center + Main.rand.NextVector2Circular(10, 10) + Vector2.Normalize(velocity) * 10, 2 + Main.rand.Next(-1, 1), velocity.RotatedByRandom(MathHelper.PiOver4) * Main.rand.NextFloat(0.3f, 1f));
        }
        return base.Shoot(player, source, position, velocity, type, damage, knockback);
    }

    const int MaxFrames = 30;
    public override void UseStyle(Player player, Rectangle heldItemFrame)
    {
        float PiecewiseLinearLerp(float value, params (float point, float time)[] segments)
        {
            if (segments.Length < 2)
                return 0f;

            float totalTime = 0f;
            for (int i = 1; i < segments.Length; i++)
            {
                totalTime += segments[i].time;
            }

            float currentTime = value * totalTime;
            float accumulatedTime = 0f;

            for (int i = 0; i < segments.Length - 1; i++)
            {
                float segmentTime = segments[i + 1].time;

                if (currentTime >= accumulatedTime && currentTime <= accumulatedTime + segmentTime)
                {
                    float localT = (currentTime - accumulatedTime) / segmentTime;
                    return MathHelper.Lerp(segments[i].point, segments[i + 1].point, localT);
                }

                accumulatedTime += segmentTime;
            }

            return segments[^1].point;
        }

        int framesFromStart = player.itemAnimationMax - player.itemAnimation;
        float percentDone = framesFromStart / (float)MaxFrames;
        float maxAngle = MathHelper.Pi / 1.5f;
        float angle = MathHelper.SmoothStep(-maxAngle, 0, PiecewiseLinearLerp(MathHelper.Clamp(percentDone, 0, 1f), (1f, 0.15f), (0f, 0.04f), (1f, 0.5f)));
        Vector2 targetAngle = player.Center.DirectionTo(Main.MouseWorld);
        player.direction = Utils.ToDirectionInt(targetAngle.ToRotation().ToRotationVector2().X > 0);
        targetAngle = targetAngle.RotatedBy(angle * player.direction);
        if (player.direction > 0)
        {
            player.itemRotation = targetAngle.ToRotation();
        }
        else
        {
            player.itemRotation = player.itemRotation = targetAngle.ToRotation() + MathHelper.Pi;
        }

        player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter, targetAngle.ToRotation() - MathHelper.PiOver2);
        player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, targetAngle.ToRotation() - MathHelper.PiOver2);

        Vector2 consistentCenterAnchor = player.itemRotation.ToRotationVector2() * (heldItemFrame.Size().X / -2f - 10f) * player.direction;

        Vector2 offsetOrigin = new Vector2(-(heldItemFrame.Size().X / 2), -(heldItemFrame.Size().Y / 2 - 2));

        offsetOrigin.X += MathHelper.SmoothStep(-10, 10, percentDone);
        offsetOrigin.X *= player.direction;
        offsetOrigin.Y *= player.gravDir;

        Vector2 consistentAnchor = consistentCenterAnchor - offsetOrigin.RotatedBy(player.itemRotation);

        Vector2 offsetAgain = heldItemFrame.Size() * -0.5f;

        Vector2 desiredPosition = player.MountedCenter + new Vector2(-2f * player.direction, -1f * player.gravDir);

        Vector2 offset = desiredPosition + offsetAgain + consistentAnchor + (heldItemFrame.Size() * 0.5f);

        player.itemLocation = offset + targetAngle.ToRotation().ToRotationVector2();

        player.FlipItemLocationAndRotationForGravity();
        base.UseStyle(player, heldItemFrame);
    }

    public override void UseItemFrame(Player player)
    {
        base.UseItemFrame(player);
    }

    public override Vector2? HoldoutOffset()
    {

        return new Vector2(-10, 0);
    }
}

public class NeanderthalBlasterProjectile : ModProjectile
{
    public override string Texture => Assets.Images.Content.Items.Weapons.Misc.StupidGun_Projectiles.KEY;
    enum Form
    {
        Pebble, Splinter, Stone, Critter
    }

}

public class NeanderthalBlasterSDFParticles : ModSystem
{
    private static Memory<Particle> particles = new Particle[100];
    private struct Particle
    {
        public Vector2 position;
        public Vector3 color;
        public Vector2 velocity;
        public float maxSize;
        public int timeAlive;
        public int maxTimeAlive;
    }
    private static WrapperShaderData<Assets.Shaders.Misc.BlasterSDF.Parameters>? metaballShader;
    private static WrapperShaderData<Assets.Shaders.Misc.GaussianBloom.Parameters>? bloomShader;
    private static RenderTarget2D? buffer;
    public override void Load()
    {
        metaballShader = Assets.Shaders.Misc.BlasterSDF.CreateSDFShader();
        bloomShader = Assets.Shaders.Misc.GaussianBloom.CreateBloomShader();

        metaballShader.Parameters.Particles = new Vector4[100];
        metaballShader.Parameters.ParticleColors = new Vector3[100];

        Main.QueueMainThreadAction(() =>
        {
            buffer = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.screenWidth / 2, Main.screenHeight / 2, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        });

        base.Load();
    }
    private static Texture2D Noise => ModContent.Request<Texture2D>(Assets.Images.Particles.Circular.KEY).Value;

    public static void AddParticle(Vector2 position, int size, Vector2 velocity = default)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles.Span[i].timeAlive <= 0)
            {
                int number = Main.rand.Next(20, 150);
                particles.Span[i] = new Particle()
                {
                    position = position,
                    maxSize = size,
                    timeAlive = number,
                    maxTimeAlive = number,
                    color = new Vector3(0.3f, 0.3f, 0.4f),
                    velocity = velocity == default ? Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(1.5f, 8f) : velocity
                };
                break;
            }
        }
    }

    private static Vector2 ScreenNormalizePosition(Vector2 position)
    {
        position.X = (position.X - Main.screenPosition.X) / Main.screenWidth;
        position.Y = (position.Y - Main.screenPosition.Y) / Main.screenHeight;
        return position;
    }

    static Vector4[] shaderParticles = new Vector4[100];
    static Vector3 startColor = new Vector3(255f / 255f, 67f / 255f, 0.0f);
    static Vector3 endColor = new Vector3((51 / 255f), 0f, 0f);
    public override void PostUpdateDusts()
    {
        MakeShaderParticlesUpToDate();
        base.PostUpdateDusts();
    }
    private static void MakeShaderParticlesUpToDate()
    {
        Debug.Assert(metaballShader is not null);
        Debug.Assert(metaballShader.Parameters.Particles is not null);
        Debug.Assert(metaballShader.Parameters.ParticleColors is not null);

        for (int i = 0; i < particles.Length; i++)
        {
            if (particles.Span[i].timeAlive > 0)
            {
                particles.Span[i].timeAlive--;
                particles.Span[i].position += particles.Span[i].velocity;
                particles.Span[i].velocity.Y -= 0.05f;
                particles.Span[i].velocity *= Main.rand.NextFloat(0.97f, 0.995f);
                particles.Span[i].color = Vector3.Lerp(endColor, startColor, particles.Span[i].timeAlive / (float)particles.Span[i].maxTimeAlive);

                if (particles.Span[i].timeAlive % 15 == 0 && Main.rand.NextBool(2))
                {
                    Color multipliedColor = new Color(particles.Span[i].color.X, particles.Span[i].color.Y, particles.Span[i].color.Z);
                    Dust.NewDustPerfect(particles.Span[i].position + Main.rand.NextVector2Circular(5f, 5f), DustID.Smoke, Vector2.Zero, 150, multipliedColor, 1f);
                }

                Lighting.AddLight(particles.Span[i].position, particles.Span[i].color * 0.5f);

                if (particles.Span[i].timeAlive <= 0)
                {
                    particles.Span[i].timeAlive = 0;

                }

                var quotient = particles.Span[i].timeAlive / (float)particles.Span[i].maxTimeAlive;
                metaballShader.Parameters.Particles[i] = new Vector4(ScreenNormalizePosition(particles.Span[i].position), MathHelper.SmoothStep(0, particles.Span[i].maxSize, quotient), particles.Span[i].timeAlive / (float)particles.Span[i].maxTimeAlive);
                metaballShader.Parameters.ParticleColors[i] = particles.Span[i].color;
            }
        }
    }
    public override void PostDrawTiles()
    {


        Debug.Assert(metaballShader is not null);
        Debug.Assert(buffer is not null);

        metaballShader.Parameters.uSource = new Vector4(buffer.Width, buffer.Height, 0, 0);
        metaballShader.Parameters.uPixel = 2f;
        metaballShader.Apply();
        var instance = Main.graphics;

        RtContentPreserver.ApplyToBindings(instance.GraphicsDevice.GetRenderTargets());

        var rts = instance.GraphicsDevice.GetRenderTargets();
        RtContentPreserver.ApplyToBindings(rts);

        instance.GraphicsDevice.SetRenderTarget(buffer);
        instance.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin(
        SpriteSortMode.Immediate,
        BlendState.AlphaBlend,
        SamplerState.PointClamp,
        DepthStencilState.Default,
        RasterizerState.CullNone,
        metaballShader.Shader);
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, Main.screenWidth / 2, Main.screenHeight / 2), new Rectangle(0, 0, Main.screenWidth / 2, Main.screenHeight / 2), Color.White);
        Main.spriteBatch.End();

        instance.GraphicsDevice.SetRenderTargets(rts);



        Debug.Assert(bloomShader is not null);
        bloomShader.Parameters.uSource = new Vector4(buffer.Width, buffer.Height, 0, 0);
        bloomShader.Parameters.passes = 8;
        bloomShader.Apply();

        Main.spriteBatch.Begin(
        SpriteSortMode.Immediate,
        BlendState.AlphaBlend,
        SamplerState.PointClamp,
        DepthStencilState.Default,
        RasterizerState.CullNone,
        null);
        Main.spriteBatch.Draw(buffer, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), new Rectangle(0, 0, buffer.Width, buffer.Height), Color.White);
        Main.spriteBatch.End();

        Main.spriteBatch.Begin(
        SpriteSortMode.Immediate,
        BlendState.Additive,
        SamplerState.PointClamp,
        DepthStencilState.Default,
        RasterizerState.CullNone,
        bloomShader.Shader);
        Main.spriteBatch.Draw(buffer, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), new Rectangle(0, 0, buffer.Width, buffer.Height), Color.White);
        Main.spriteBatch.End();

        base.PostDrawTiles();
    }
}
