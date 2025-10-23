using System;
using System.IO;
using Cataphract.Common.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

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
    }

    public override void AI()
    {
        if (_data.bossTimer++ == 0)
        {
            _data.ModNPC = this;
            StateController.PushState(new InitTransitionState());
        }

        if (!StateController.Update(_data))
        {
            StateController.PopCurState();
        }

        base.AI();
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (currentState is not null)
        {
            BossState.StatelessDrawActions?.Invoke(spriteBatch, this, NPC.position - screenPos);
        }

        return base.PreDraw(spriteBatch, screenPos, drawColor);
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