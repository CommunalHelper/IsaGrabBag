using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.ModInterop;
using System;

namespace Celeste.Mod.IsaGrabBag {
    public class GrabBagModule : EverestModule {
        public GrabBagModule() {
            Instance = this;
        }

        private static GrabBagMeta gbMeta;
        public static GrabBagMeta GrabBagMeta {
            get {
                if (gbMeta == null) {

                    AreaKey key;
                    Level level = Engine.Scene as Level;
                    level ??= Engine.NextScene as Level;

                    if (level != null) {
                        key = level.Session.Area;
                    } else {
                        LevelLoader loader = Engine.Scene as LevelLoader;
                        key = loader.Level.Session.Area;
                    }

                    gbMeta = GrabBagMeta.Default(key);
                };
                return gbMeta;
            }
        }

        public override Type SessionType => typeof(IsaSession);
        public static IsaSession Session => (IsaSession)Instance._Session;

        public static GrabBagModule Instance { get; private set; }

        public static Player playerInstance { get; private set; }

        public static SpriteBank sprites { get; private set; }

        public static MonoMod.Utils.DynamicData BingoUIModuleSettings;

        public override void Load() {
            typeof(GravityHelperImports.Interop).ModInterop();
            typeof(ReverseHelperImports.Interop).ModInterop();

            ArrowBubble.Load();
            BadelineFollower.Load();
            DreamSpinnerRenderer.Load();
            ForceVariants.Load();
            RewindCrystal.Load();
            ZipLine.Load();

            Everest.Events.Level.OnTransitionTo += Level_OnTransitionTo;
            Everest.Events.Level.OnEnter += Level_OnEnter;
            Everest.Events.Level.OnExit += Level_OnExit;
            Everest.Events.LevelLoader.OnLoadingThread += LevelLoader_OnLoadingThread;
            Everest.Events.Player.OnSpawn += Player_OnSpawn;

            if (Everest.Loader.TryGetDependency(new() { Name = "BingoUI", Version = new(1, 2, 6) }, out var BingoUIModule))
                BingoUIModuleSettings = MonoMod.Utils.DynamicData.For(BingoUIModule._Settings);
        }

        public override void Unload() {
            ArrowBubble.Unload();
            BadelineFollower.Unload();
            DreamSpinnerRenderer.Unload();
            ForceVariants.Unload();
            RewindCrystal.Unload();
            ZipLine.Unload();

            Everest.Events.Level.OnTransitionTo -= Level_OnTransitionTo;
            Everest.Events.Level.OnEnter -= Level_OnEnter;
            Everest.Events.Level.OnExit -= Level_OnExit;
            Everest.Events.LevelLoader.OnLoadingThread -= LevelLoader_OnLoadingThread;
            Everest.Events.Player.OnSpawn -= Player_OnSpawn;
        }

        public override void LoadContent(bool firstLoad) {
            RewindCrystal.LoadGraphics();
            sprites = new SpriteBank(GFX.Game, "Graphics/IsaGrabBag.xml");
        }

        private void Player_OnSpawn(Player player) {
            Level lvl = player.SceneAs<Level>();

            playerInstance = player;

            ForceVariants.GetFromSession();

            if (player.Get<WaterBoostHandler>() == null) {
                player.Add(new WaterBoostHandler());
            }

            if (lvl.Session.GetFlag(BadelineFollower.IsaGrabBag_HasBadelineFollower)) {
                foreach (BadelineBoost boost in lvl.Entities.FindAll<BadelineBoost>()) {
                    boost.Visible = false;
                    boost.Collidable = false;
                }

                if (lvl.Entities.FindFirst<BadelineFollower>() == null) {
                    if (BadelineFollower.instance == null) {
                        BadelineFollower follower = new(lvl, player.Position + new Vector2((int)playerInstance.Facing * -12, -20));
                        lvl.Add(follower);
                        player.Leader.GainFollower(follower.follower);
                    } else {
                        BadelineFollower.instance.Readd(lvl, player);
                    }
                }
            }

            if (lvl.Session.GetFlag(BadelineFollower.IsaGrabBag_HasBadelineFollower)) {
                BadelineFollower.instance.dummy.Visible = true;
            }

            BadelineFollower.CheckBooster(lvl, false);
        }

        private void Level_OnExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
            gbMeta = null;

            ForceVariants.ResetSession();

            if (BadelineFollower.instance != null) {
                BadelineFollower.instance.RemoveSelf();
            }

            BadelineFollower.instance = null;
        }

        private void LevelLoader_OnLoadingThread(Level level) {
            Session session = level.Session;

            try {
                gbMeta = null;

                //string s = session.Area.GetSID();
                if (session != null && session.Area != null && session.Area.SID != null) {

                    ModAsset get = Everest.Content.Get(session.Area.SID);
                    if (get != null && get.TryGetMeta(out GrabBagWrapperMeta parsed)) {
                        gbMeta = parsed.IsaGrabBag;
                    }
                }
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "IsaGrabBag", "Unable to properly get metadata");
                Logger.LogDetailed(e);
            }

            gbMeta ??= GrabBagMeta.Default(session.Area);

            if (session.Area.LevelSet.StartsWith("SpringCollab2020")) {
                GrabBagMeta.WaterBoost = true;
            }
        }

        private void Level_OnEnter(Session session, bool fromSaveData) {
            ForceVariants.GetDefaults();
            ForceVariants.ReinforceSession();
        }

        private void Level_OnTransitionTo(Level level, LevelData next, Vector2 direction) {
            ForceVariants.SaveToSession();

            if (GrabBagMeta == null) {
                gbMeta = GrabBagMeta.Default(level.Session.Area);
            }

            if (level.Session.GetFlag(BadelineFollower.IsaGrabBag_HasBadelineFollower)) {
                if (BadelineFollower.instance == null) {
                    BadelineFollower follower = new(level, playerInstance.Position);
                    level.Add(follower);
                    playerInstance.Leader.GainFollower(follower.follower);

                    BadelineFollower.CheckBooster(level, false);
                }

                BadelineFollower.Search();

                if (level.Session.GetFlag(BadelineFollower.IsaGrabBag_HasBadelineFollower)) {
                    BadelineFollower.instance.dummy.Visible = true;
                }
            }
        }
    }
}
