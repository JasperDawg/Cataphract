using System;
using System.Diagnostics;
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
        Item.UseSound = SoundID.Item11;
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
        for (int i = 0; i < 5; i++) 
        {
            NeanderthalBlasterSDFParticles.AddParticle(player.Center + Main.rand.NextVector2Circular(40, 40), 30 + Main.rand.Next(-20, 20));
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
        public int size;
        public int timeAlive;
    }
    private static WrapperShaderData<Assets.Shaders.Misc.BlasterSDF.Parameters>? metaballShader;
    public override void Load()
    {
        metaballShader = Assets.Shaders.Misc.BlasterSDF.CreateSDFShader();
        metaballShader.Parameters.Particles = new Vector4[100];
        metaballShader.Parameters.ParticleColors = new Vector3[100];

        base.Load();
    }
    private static Texture2D Noise => ModContent.Request<Texture2D>(Assets.Images.Particles.Circular.KEY).Value;

    public static void AddParticle(Vector2 position, int size)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles.Span[i].timeAlive <= 0)
            {
                particles.Span[i] = new Particle()
                {
                    position = position,
                    size = size,
                    timeAlive = 60,
                    color = new Vector3(Main.rand.NextFloat(), Main.rand.NextFloat(), Main.rand.NextFloat())
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

    private static void MakeShaderParticlesUpToDate()
    {
        Debug.Assert(metaballShader is not null);
        Debug.Assert(metaballShader.Parameters.Particles is not null);
        Debug.Assert(metaballShader.Parameters.ParticleColors is not null);

        for (int i = 0; i < particles.Length; i++)
        {
            if (particles.Span[i].timeAlive > 0)
            {
                metaballShader.Parameters.Particles[i] = new Vector4(ScreenNormalizePosition(particles.Span[i].position), particles.Span[i].size, particles.Span[i].timeAlive);
                metaballShader.Parameters.ParticleColors[i] = particles.Span[i].color;
                particles.Span[i].timeAlive--;
                particles.Span[i].size = (int)(particles.Span[i].size * 0.99f);
            }
        }
    }
    public override void PostDrawTiles()
    {
        MakeShaderParticlesUpToDate();

        Debug.Assert(metaballShader is not null);
        metaballShader.Parameters.uSource = new Vector4(Main.screenWidth, Main.screenHeight, 0, 0);
        metaballShader.Apply();

        Main.spriteBatch.Begin(
        SpriteSortMode.Immediate,
        BlendState.AlphaBlend,
        SamplerState.PointClamp,
        DepthStencilState.Default,
        RasterizerState.CullNone,
        metaballShader.Shader);
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
        Main.spriteBatch.End();
        base.PostDrawTiles();
    }
}
