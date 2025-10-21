using System;
using Cataphract.Common.Easing;
using JetBrains.Annotations;
using Microsoft.Build.Evaluation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Cataphract.Common.Rendering;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;
using Cataphract.Core;
using System.Diagnostics;
using ReLogic.Content;
using Terraria.Audio;

namespace Cataphract.Content.Items;

public class ShinyRockSceptor : ModItem
{

    public override string Texture => Assets.Images.Content.Items.Weapons.Misc.GeodeWand.KEY;

    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 40;
        Item.damage = 12;
        Item.DamageType = DamageClass.Magic;
        Item.mana = 8;

        Item.useStyle = -1;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.UseSound = Assets.Audio.Misc.StoneWand_Shoot1.Asset with
        {
            Volume = 3.5f,
            PitchVariance = 0.4f
        };

        Item.noUseGraphic = true;
        Item.noMelee = true;
        Item.autoReuse = true;

        Item.shoot = ModContent.ProjectileType<ShinyRockSceptor_Projectile>();
        Item.shootSpeed = 8f;

        base.SetDefaults();
    }

    public override bool CanUseItem(Player player)
    {
        if (player.ownedProjectileCounts[Item.shoot] >= 1)
            return false;
        return base.CanUseItem(player);
    }

    const int rockXOffset = 11;
    const int rockYOffset = -23;

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
    {
        Projectile.NewProjectile(player.GetSource_ItemUse(Item), position, velocity, ModContent.ProjectileType<ShinyRockSceptor_Hitscan>(), damage, knockback, player.whoAmI);
        base.ModifyShootStats(player, ref position, ref velocity, ref type, ref damage, ref knockback);
    }

    const int MaxFrames = 30;

    public override void UseStyle(Player player, Rectangle heldItemFrame)
    {


        float percentDone = player.itemAnimation / (float)player.itemAnimationMax;

        float angle = Easing.PiecewiseLinearLerp(MathHelper.SmoothStep(0, 1, 1f - percentDone), (0f, 0.25f), (MathHelper.ToRadians(15f), 0.25f), (MathHelper.ToRadians(45f), 0.5f), (MathHelper.ToRadians(-25f), 0.5f), (0f, 2.75f));
        Vector2 targetAngle = player.Center.DirectionTo(Main.MouseWorld);
        player.direction = Utils.ToDirectionInt(targetAngle.ToRotation().ToRotationVector2().X > 0);


        player.itemRotation = targetAngle.ToRotation();
        targetAngle = targetAngle.RotatedBy(angle * player.direction);


        player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, targetAngle.ToRotation() - MathHelper.PiOver4 * (player.direction == 1 ? 1 : 3));
        player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, targetAngle.ToRotation() - MathHelper.PiOver2);

        player.FlipItemLocationAndRotationForGravity();
        base.UseStyle(player, heldItemFrame);
    }
}

public class ShinyRockSceptor_Projectile : ModProjectile
{
    public override string Texture => Assets.Images.Content.Items.Weapons.Misc.GeodeWand.KEY;

    public override void SetDefaults()
    {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.penetrate = 1;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.timeLeft = 30;
        Projectile.aiStyle = -1;
        Projectile.tileCollide = false;
    }

    public override bool PreAI()
    {
        Player player = Main.player[Projectile.owner]; // Since we access the owner player instance so much, it's useful to create a helper local variable for this

        int duration = player.itemAnimationMax; // Define the duration the projectile will exist in frames

        player.heldProj = Projectile.whoAmI; // Update the player's held projectile id


        // Reset projectile time left if necessary
        if (Projectile.timeLeft > duration)
        {
            Projectile.timeLeft = duration;
        }

        // todo: sync this
        Projectile.Center = player.MountedCenter + player.DirectionTo(Main.MouseWorld) * Projectile.width / 2f;

        // Apply proper rotation to the sprite.
        Projectile.rotation = player.itemRotation;

        bool flip = Projectile.rotation.ToRotationVector2().X < 0;
        float offsetMultiplier = flip ? -1f : 1f;

        Vector2 rockOffset = new Vector2(rockXOffset * offsetMultiplier, rockYOffset).RotatedBy(Projectile.rotation);
        if (flip)
            rockOffset *= -1;

        if (player.itemAnimation == player.itemAnimationMax - 1)
        {
            for (int i = 0; i < 10; i++)
            {

                Vector2 dustPosition = Projectile.Center + rockOffset + Vector2.UnitX.RotatedByRandom(MathHelper.TwoPi) * Main.rand.NextFloat(8f, 16f);

                Vector2 dustVelocity = (dustPosition - Projectile.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f);
                var dust = Dust.NewDustPerfect(dustPosition, DustID.GemAmethyst, dustVelocity, Scale: 1.2f);
                dust.noGravity = true;
            }
        }
        return false; // Don't execute vanilla AI.
    }
    const int rockXOffset = 11;
    const int rockYOffset = -23;

    public override bool PreDraw(ref Color lightColor)
    {
        Rectangle frame = new Rectangle(0, 0, TextureAssets.Projectile[Projectile.type].Value.Width / 2, TextureAssets.Projectile[Projectile.type].Value.Height);
        Rectangle glowFrame = new Rectangle(TextureAssets.Projectile[Projectile.type].Value.Width / 2, 0, TextureAssets.Projectile[Projectile.type].Value.Width / 2, TextureAssets.Projectile[Projectile.type].Value.Height);

        var circleGlow = Assets.Images.Particles.Star.Asset.Value;
        var rockTexture = Assets.Images.Content.Items.Weapons.Misc.GeodeWand_Stone.Asset.Value;
        Rectangle rockFrame = new Rectangle(0, 0, rockTexture.Width / 2, rockTexture.Height);
        Rectangle rockGlowFrame = new Rectangle(rockTexture.Width / 2, 0, rockTexture.Width / 2, rockTexture.Height);

        bool flip = Projectile.rotation.ToRotationVector2().X < 0;
        SpriteEffects effects = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;

        float timeLeftQuotient = MathHelper.Clamp(Projectile.timeLeft / (float)(Main.player[Projectile.owner].itemAnimationMax), 0f, 1f);

        Vector2 wandOffset = Vector2.Zero;
        float wandRotationOffset = Easing.PiecewiseLinearLerp(MathHelper.SmoothStep(0, 1, 1f - timeLeftQuotient), (0f, 0.25f), (MathHelper.ToRadians(30f), 0.25f), (MathHelper.ToRadians(-20f), 0.5f), (0f, 2.75f));

        float newWandRot = flip ? Projectile.rotation - wandRotationOffset : Projectile.rotation + wandRotationOffset;

        wandOffset.X = Easing.PiecewiseLinearLerp(MathHelper.SmoothStep(0, 1, 1f - timeLeftQuotient), (0f, 0.25f), (5f, 0.25f), (-8f, 0.5f), (0f, 2.75f));
        wandOffset.Y = Easing.PiecewiseLinearLerp(MathHelper.SmoothStep(0, 1, 1f - timeLeftQuotient), (0f, 0.25f), (-4f, 0.25f), (4f, 0.5f), (0f, 2.75f));

        if (flip)
            wandOffset.X *= -1;
        float ease = Easing.InOutSine(timeLeftQuotient * 1f);
        Color glowColor = Color.Lerp(Color.White, Color.White * 20, ease);
        Main.EntitySpriteDraw(TextureAssets.Projectile[Projectile.type].Value, Projectile.Center - Main.screenPosition + wandOffset, frame, lightColor, newWandRot, frame.Size() / 2f, 1f, effects, 0);
        Main.EntitySpriteDraw(TextureAssets.Projectile[Projectile.type].Value, Projectile.Center - Main.screenPosition + wandOffset, glowFrame, glowColor, newWandRot, glowFrame.Size() / 2f, 1f, effects, 0);

        int vertOffset = (int)Math.Abs(MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 4f);
        int horizontalOffset = (int)(MathF.Cos(Main.GlobalTimeWrappedHourly * 6f) * 2f);

        float offset = rockXOffset + horizontalOffset + Easing.PiecewiseLinearLerp(MathHelper.SmoothStep(0, 1, 1f - timeLeftQuotient), (0f, 0.25f), (10f, 0.25f), (-40f, 0.5f), (0f, 2.75f));
        float rockRotationOffset = Easing.PiecewiseLinearLerp(MathHelper.SmoothStep(0, 1, 1f - timeLeftQuotient), (0f, 0.25f), (MathHelper.ToRadians(45f), 0.25f), (MathHelper.ToRadians(-60f), 0.5f), (0f, 2.75f));

        var rockOffset = new Vector2(flip ? -offset : offset, rockYOffset + -vertOffset).RotatedBy(Projectile.rotation);

        if (flip)
            rockOffset *= -1;

        float newRot = flip ? Projectile.rotation - rockRotationOffset : Projectile.rotation + rockRotationOffset;

        Main.spriteBatch.End(out var ss);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Additive,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null
        );

        Main.spriteBatch.Draw(circleGlow, Projectile.Center - Main.screenPosition + rockOffset, null, Color.Purple, MathF.PI * Easing.InOutSine(timeLeftQuotient * 0.5f), circleGlow.Size() / 2f, 0.2f * ease, SpriteEffects.None, 0f);
        Main.spriteBatch.Restart(ss);

        Main.EntitySpriteDraw(rockTexture, Projectile.Center - Main.screenPosition + rockOffset, rockFrame, lightColor, newRot, rockFrame.Size() / 2f, 1f, effects, 0);
        Main.EntitySpriteDraw(rockTexture, Projectile.Center - Main.screenPosition + rockOffset, rockGlowFrame, glowColor, newRot, rockGlowFrame.Size() / 2f, 1f, effects, 0);

        return false;
    }
}

public class ShinyRockSceptor_Hitscan : ModProjectile
{
    public override string Texture => Assets.Images.Content.Items.Weapons.Misc.GeodeWand.KEY;
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Projectile.type] = 1000;
        ProjectileID.Sets.TrailingMode[Projectile.type] = 3;
        base.SetStaticDefaults();
    }
    public override void SetDefaults()
    {
        Projectile.width = 1;
        Projectile.height = 1;
        Projectile.friendly = true;
        Projectile.penetrate = 1;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.timeLeft = 600;
        Projectile.aiStyle = -1;
        Projectile.extraUpdates = 6;
        Projectile.tileCollide = true;
    }

    public override void AI()
    {
        Projectile.velocity.Y += 0.04f;
    }

    public override void OnKill(int timeLeft)
    {
        for (int i = 0; i < 20; i++)
        {
            Vector2 dustPosition = Projectile.Center + Vector2.UnitX.RotatedByRandom(MathHelper.TwoPi) * Main.rand.NextFloat(8f, 16f);
            Vector2 dustVelocity = (dustPosition - Projectile.Center).SafeNormalize(Vector2.Zero) * 2;


            var dust2 = Dust.NewDustPerfect(dustPosition, DustID.GemAmethyst, -Projectile.velocity * Main.rand.NextFloat(0.4f, 3f), Scale: Main.rand.NextFloat(1.2f, 1.5f));

            dust2.noGravity = true;
        }
        base.OnKill(timeLeft);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        VertexStrip vertexStrip = new VertexStrip();

        MiscShaderData miscShaderData = GameShaders.Misc["LightDisc"];
        miscShaderData.UseSaturation(-2f);
        miscShaderData.UseOpacity(MathHelper.Lerp(4f, 8f, 1f));
        miscShaderData.Apply();
        vertexStrip.PrepareStripWithProceduralPadding(
            Projectile.oldPos,
            Projectile.oldRot,
            (i) => Color.Lerp(new Color(255, 0, 100, 0), Color.LightPink, 1 - Easing.InOutSine(i + 0.3f)),
            (i) => 4f * (i * Easing.PiecewiseLinearLerp(i, (0.0f, 1f), (1.4f, 0.5f), (1f, 1f), (0f, 1f))),
            -Main.screenPosition + Projectile.Size / 2f,
            false,
            default
            );

        vertexStrip.DrawTrail();
        Main.pixelShader.CurrentTechnique.Passes[0].Apply();

        Texture2D circleGlow = Assets.Images.Particles.Circular.Asset.Value;
        Texture2D starGlow = Assets.Images.Particles.Star.Asset.Value;
        Main.spriteBatch.End(out var ss);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Additive,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null
        );

        Main.spriteBatch.Draw(circleGlow, Projectile.Center - Main.screenPosition, null, Color.Purple * 0.8f, MathF.PI, circleGlow.Size() / 2f, 0.2f, SpriteEffects.None, 0f);
        Main.spriteBatch.Draw(starGlow, Projectile.Center - Main.screenPosition, null, Color.Pink * 0.5f, MathF.Sin(Main.GlobalTimeWrappedHourly * 6f) * 6f, starGlow.Size() / 2f, 0.15f, SpriteEffects.None, 0f);
        Main.spriteBatch.Draw(starGlow, Projectile.Center - Main.screenPosition, null, Color.White, 0f, starGlow.Size() / 2f, 0.1f, SpriteEffects.None, 0f);
        Main.spriteBatch.Restart(ss);

        return false;
    }
}

public class ShinyRockSceptor_GlobalNPC : GlobalNPC {
    int hitCount = 0;
    const int maxHits = 5;
    int timeSinceLastHit = 0;
    const int maxTimeSinceLastHit = 300;
    float hitCountQuotient => (float)hitCount / maxHits;

    public override bool InstancePerEntity => true;

    public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        if (projectile.ModProjectile is ShinyRockSceptor_Hitscan)
        {
            timeSinceLastHit = maxTimeSinceLastHit;
            hitCount++;
            if (hitCount >= maxHits)
            {
                hitCount = 0;
                Vector2 spawnPosition = npc.Center + new Vector2(Main.rand.NextFloat(-npc.width / 2f, npc.width / 2f), Main.rand.NextFloat(-npc.height / 2f, npc.height / 2f));
                Projectile.NewProjectile(npc.GetSource_OnHit(npc), spawnPosition, Vector2.Zero, ModContent.ProjectileType<RockSceptor_Explosion>(), (int)(projectile.damage * 3f), 0f, projectile.owner);
                
                for (int i = 0; i < 20; i++) {
                    ShinyRockSceptor_Particles.AddParticle(spawnPosition + Main.rand.NextVector2Circular(10f, 10f), Main.rand.Next(4, 16), (Vector2.One * 20f).RotatedByRandom(MathHelper.TwoPi));
                }

                SoundEngine.PlaySound(SoundID.Item14, spawnPosition);    
            }
        }

        base.OnHitByProjectile(npc, projectile, hit, damageDone);
    }

    public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Texture2D circleGlow = Assets.Images.Particles.Star.Asset.Value;
        if (timeSinceLastHit <= 0)
            return base.PreDraw(npc, spriteBatch, screenPos, drawColor);

        float ease = Easing.InOutSine((timeSinceLastHit -= 4) / (float)maxTimeSinceLastHit);

        Main.spriteBatch.End(out var ss);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Additive,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null
        );

        Main.spriteBatch.Draw(circleGlow, npc.Center - Main.screenPosition, null, Color.HotPink * 0.8f * ease * (1.5f * hitCountQuotient), MathF.PI * hitCountQuotient, circleGlow.Size() / 2f, 0.4f * ease * (1.5f * hitCountQuotient), SpriteEffects.None, 0f);
        Main.spriteBatch.Restart(ss);

        return base.PreDraw(npc, spriteBatch, screenPos, drawColor);
    }
}

public class RockSceptor_Explosion : ModProjectile
{
    public override string Texture => Assets.Images.Content.Items.Weapons.Misc.GeodeWand.KEY;

    public override void SetDefaults()
    {
        Projectile.width = 100;
        Projectile.height = 100;
        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.timeLeft = 30;
        Projectile.aiStyle = -1;
        Projectile.alpha = 255;
        Projectile.tileCollide = false;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        
        return base.PreDraw(ref lightColor);
    }
}

public class ShinyRockSceptor_Particles : ModSystem
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
    private static WrapperShaderData<Assets.Shaders.Misc.RockSceptorSDF.Parameters>? metaballShader;
    private static WrapperShaderData<Assets.Shaders.Misc.GaussianBloom.Parameters>? bloomShader;
    private static RenderTarget2D? buffer;
    public override void Load()
    {
        metaballShader = Assets.Shaders.Misc.RockSceptorSDF.CreateSDFShader();
        bloomShader = Assets.Shaders.Misc.GaussianBloom.CreateBloomShader();

        metaballShader.Parameters.Particles = new Vector4[100];
        metaballShader.Parameters.ParticleColors = new Vector3[100];
        
        
        Main.QueueMainThreadAction(() =>
        {
            buffer = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.screenWidth / 2, Main.screenHeight / 2, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        });

        base.Load();
    }
    private static Asset<Texture2D> Noise => Assets.Images.Noise.Noise_DomainWarp_1.Asset;

    public static void AddParticle(Vector2 position, int size, Vector2 velocity = default)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles.Span[i].timeAlive <= 0)
            {
                int number = Main.rand.Next(20, 80);
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

    static Vector3 startColor = new Vector3(1, 0.8f, 1f);
    static Vector3 endColor = new Vector3((51 / 255f), -1f, 0.8f);
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
                particles.Span[i].velocity *= Main.rand.NextFloat(0.60f, 0.85f);
                particles.Span[i].color = Vector3.Lerp(endColor, startColor, particles.Span[i].timeAlive / (float)particles.Span[i].maxTimeAlive);

                if (particles.Span[i].timeAlive % 15 == 0 && Main.rand.NextBool(2))
                {
                    Color multipliedColor = new Color(particles.Span[i].color.X, particles.Span[i].color.Y, particles.Span[i].color.Z);
                    Dust.NewDustPerfect(particles.Span[i].position + Main.rand.NextVector2Circular(5f, 5f), DustID.GemAmethyst, Vector2.One.RotatedByRandom(MathHelper.TwoPi), 150, multipliedColor, 1f);
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

        metaballShader.Parameters.uSource = new Vector4(buffer.Width, buffer.Height, Main.screenPosition.X, Main.screenPosition.Y);
        metaballShader.Parameters.uPixel = 2f;
        metaballShader.Parameters.uTexture1 = Noise.Value;

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

        Main.spriteBatch.Begin(
        SpriteSortMode.Immediate,
        BlendState.AlphaBlend,
        SamplerState.PointClamp,
        DepthStencilState.Default,
        RasterizerState.CullNone,
        null);
        Main.spriteBatch.Draw(buffer, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), new Rectangle(0, 0, buffer.Width, buffer.Height), Color.White);
        Main.spriteBatch.End();

        base.PostDrawTiles();
    }
}