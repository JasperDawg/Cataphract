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

namespace Cataphract.Content.Items;

public class ShinyRockSceptor : ModItem
{

    public override string Texture => Assets.Images.Content.Items.Weapons.Misc.GeodeWand.KEY;

    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 40;

        Item.useStyle = -1;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.UseSound = Assets.Audio.Misc.StoneWand_Shoot1.Asset with
        {
            pitchVariance = 0.4f
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
        Projectile.penetrate = -1;
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