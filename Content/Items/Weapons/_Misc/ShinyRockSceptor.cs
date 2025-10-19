using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

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
        Item.UseSound = Assets.Audio.Misc.StoneWand_Shoot1.Asset;

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
        float maxAngle = MathHelper.PiOver4;
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

        Projectile.velocity = Vector2.Normalize(Projectile.velocity); // Velocity isn't used in this spear implementation, but we use the field to store the spear's attack direction.

        float halfDuration = duration * 0.5f;
        float progress;

        // Here 'progress' is set to a value that goes from 0.0 to 1.0 and back during the item use animation.
        if (Projectile.timeLeft < halfDuration)
        {
            progress = Projectile.timeLeft / halfDuration;
        }
        else
        {
            progress = (duration - Projectile.timeLeft) / halfDuration;
        }

        // Move the projectile from the HoldoutRangeMin to the HoldoutRangeMax and back, using SmoothStep for easing the movement
        Projectile.Center = player.MountedCenter + Projectile.velocity * 2f;

        // Apply proper rotation to the sprite.
        if (Projectile.spriteDirection == -1)
        {
            // If sprite is facing left, rotate 45 degrees
            Projectile.rotation += MathHelper.ToRadians(45f);
        }
        else
        {
            // If sprite is facing right, rotate 135 degrees
            Projectile.rotation += MathHelper.ToRadians(135f);
        }

        return false; // Don't execute vanilla AI.
    }
}