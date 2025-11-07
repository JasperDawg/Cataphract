using System;
using System.Diagnostics;
using System.IO;
using Cataphract.Common.Utilities;
using Cataphract.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Cataphract.Common.Rendering;

namespace Cataphract.Content.NPCs;

internal struct MagicScientistData
{
    public int bossTimer;
    public int phase;
    public MagicScientist ModNPC { get; set; }
    public NPC ThisNPC => ModNPC.NPC;
    public int npcID => ThisNPC.whoAmI;
    public Vector2 curPosition => ThisNPC.position;
    public bool stateDone;
    public bool shouldBeDeadNow;

}

public class MagicScientist : ModNPC
{
    internal abstract class BossState : State<MagicScientistData>
    {
        protected bool activated;
        protected int timer;
        protected int maxTime;
        internal static Action<SpriteBatch, MagicScientist, Vector2>? StatelessDrawActions;
        public abstract void Broadcast(BinaryWriter writer);
        public abstract void Listen(BinaryReader reader);
    }
    internal static float gravity = 1f;
    private MagicScientistData _data = new();
    private StateController<MagicScientistData> StateController { get; set; } = new();
    private BossState? currentState => StateController.CurrentState as BossState;

    public override string Texture => Assets.Images.Particles.Star.KEY;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[NPC.type] = 4;
        base.SetDefaults();
    }

    public override void SetDefaults()
    {
        base.SetDefaults();
        NPC.width = 40;
        NPC.height = 40;
        NPC.damage = 0;
        NPC.defense = 10;
        NPC.lifeMax = 5000;
        NPC.knockBackResist = 0f;
        NPC.aiStyle = -1;
    }

    public override void AI()
    {
        if (_data.bossTimer++ == 0)
        {
            _data.ModNPC = this;
            //StateController.PushState<InitTransitionState>(new InitTransitionState() { stateID = new StateID("InitTransitionState", 0) }); // todo: change this immediately
        }

        if (!StateController.Update(_data))
        {
            StateController.PopCurState();
        }

        base.AI();
    }
    static WrapperShaderData<Assets.Shaders.Misc.DistortionSphere.Parameters>? _distortionShader;
    static RenderTarget2D? _distortionTarget;
    public override void Load()
    {
        _distortionShader = Assets.Shaders.Misc.DistortionSphere.CreateShieldShader();
        Main.QueueMainThreadAction(() =>
        {
            _distortionTarget = new RenderTarget2D(Main.graphics.GraphicsDevice, 200, 200);
        });
        base.Load();
    }
    private static Vector2 ScreenNormalizePosition(Vector2 position)
    {
        position.X = (position.X - Main.screenPosition.X) / Main.screenWidth;
        position.Y = (position.Y - Main.screenPosition.Y) / Main.screenHeight;
        return position;
    }
    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (currentState is not null)
        {
            BossState.StatelessDrawActions?.Invoke(spriteBatch, this, NPC.position - screenPos);
        }

        Rectangle frame = new Rectangle(0, 0, 200, 200);

        
        Debug.Assert(_distortionShader is not null);

        _distortionShader.Parameters.uTime = Main.GlobalTimeWrappedHourly;
        Vector2 normalizedPos = ScreenNormalizePosition(NPC.Center);
        _distortionShader.Parameters.uSource = new Vector4(frame.Width, frame.Height, normalizedPos.X, normalizedPos.Y);
        _distortionShader.Apply();

        spriteBatch.End(out var ss);
        Main.spriteBatch.Begin(
        SpriteSortMode.Immediate,
        BlendState.Additive,
        SamplerState.PointClamp,
        DepthStencilState.Default,
        RasterizerState.CullNone,
        _distortionShader.Shader,
        Main.GameViewMatrix.EffectMatrix
        );

        spriteBatch.Draw(_distortionTarget, NPC.Center - screenPos, frame, Color.White, NPC.rotation, frame.Size() / 2, NPC.scale, SpriteEffects.None, 0);
        spriteBatch.Restart(ss);

        return false;
    }

    internal void AddState(BossState state)
    {
        if (state is not null)
        {
            state.stateData = _data;

            if (!StateController.PushState(state))
            {
                StateController.PopCurState();
            }
        }
    }

    internal void SyncState()
    {
        if (currentState is not null)
        {
            _data = currentState.stateData;
        }
    }

    internal void SyncState(BossState state)
    {
        _data = state.stateData;
    }
}

file class InitTransitionState : MagicScientist.BossState
{
    public override bool Enter(params MagicScientistData[] parameters)
    {
        return true;
    }

    public override bool Exit(params MagicScientistData[] parameters)
    {
        return true;
    }

    public override void Broadcast(BinaryWriter writer)
    {

    }
    public override void Listen(BinaryReader reader)
    {

    }

    public override bool Update(params MagicScientistData[] parameters)
    {
        stateData = parameters[0];

        stateData.shouldBeDeadNow = false;
        stateData.stateDone = true;

        PopSelf();

        stateData.ModNPC.SyncState(this);

        return true;
    }
}