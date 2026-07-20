using Celeste.Mod.Entities;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.RuntimeDetour;
using System;
using System.Collections;
using System.Reflection;

namespace Celeste.Mod.IsaGrabBag {
    [CustomEntity("isaBag/rewindCrystal")]
    public class RewindCrystal : Entity {
        private const float RewindCount = 3.5f;
        private const int rewindFrames = (int)(RewindCount * 60);

        private static Hook subHudRendererHook;

        private static readonly CharacterState[] states = new CharacterState[rewindFrames];
        private readonly bool oneUse = false;
        private static float RenderStrength;
        private static int currentFrame;
        private static int maxRewind;
        private static RenderTarget2D playerTarget;
        private static Effect glitchEffect;

        private readonly Sprite visuals;
        private readonly Image outline;
        private Level level;

        public RewindCrystal(Vector2 position)
            : base(position) {
            visuals = GrabBagModule.sprites.Create("rewind_crystal");
            outline = new Image(GFX.Game["isafriend/objects/rewind/outline00"]) {
                Position = new Vector2(-8, -8),
                Visible = false
            };

            Collider = new Hitbox(16, 16, -8, -8);

            Add(visuals);
            Add(new PlayerCollider(OnPlayer));
            Add(outline);

            Depth = 1500;
        }

        public RewindCrystal(EntityData data, Vector2 offset)
            : this(data.Position + offset) {
        }

        public static bool Rewinding { get; internal set; } = false;

        public static void ClearRewindBuffer() {
            currentFrame = 0;
            maxRewind = 0;
            states[states.Length - 1] = new CharacterState();
        }

        public static void Load() {
            On.Celeste.PlayerCollider.Check += PlayerCollider_Check;
            On.Celeste.Player.Update += Player_Update;
            On.Celeste.Player.Render += Player_Render;
            Everest.Events.Level.OnLoadLevel += Level_OnLoadLevel;

            subHudRendererHook = new Hook(
                typeof(SubHudRenderer).GetMethod("Render"),
                typeof(RewindCrystal).GetMethod("SubHudRenderer_Render", BindingFlags.NonPublic | BindingFlags.Static));
        }

        public static void Unload() {
            On.Celeste.PlayerCollider.Check -= PlayerCollider_Check;
            On.Celeste.Player.Update -= Player_Update;
            On.Celeste.Player.Render -= Player_Render;
            Everest.Events.Level.OnLoadLevel -= Level_OnLoadLevel;
            subHudRendererHook?.Dispose();
            subHudRendererHook = null;
        }

        public static void LoadGraphics() {
            playerTarget = new RenderTarget2D(Draw.SpriteBatch.GraphicsDevice, 64, 64);

            try {
                ModAsset asset = Everest.Content.Get("Effects/glitchy_effect.cso");
                glitchEffect = new Effect(Draw.SpriteBatch.GraphicsDevice, asset.Data);
            } catch (Exception e) {
                Logger.Log(LogLevel.Error, "IsaGrabBag", "Failed to load the shader");
                Logger.LogDetailed(e);
            }
        }

        public override void Added(Scene scene) {
            base.Added(scene);
            level = SceneAs<Level>();
        }

        private static void Player_Render(On.Celeste.Player.orig_Render orig, Player self) {
            if (RenderStrength > 0) {
                GameplayRenderer.End();

                Draw.SpriteBatch.GraphicsDevice.SetRenderTarget(playerTarget);
                Draw.SpriteBatch.GraphicsDevice.Clear(Color.Transparent);

                Matrix m = Matrix.CreateTranslation(-(self.X - 32), -(self.Y - 32), 0);

                Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, m);

                orig(self);

                Draw.SpriteBatch.End();

                Draw.SpriteBatch.GraphicsDevice.SetRenderTarget(GameplayBuffers.Gameplay);
                GameplayRenderer.Begin();
            } else {
                orig(self);

            }
        }

        private static void SubHudRenderer_Render(Action<SubHudRenderer, Scene> orig, SubHudRenderer self, Scene scene) {
            if (MInput.Keyboard.Pressed(Microsoft.Xna.Framework.Input.Keys.Q)) {
                LoadGraphics();
            }

            if (scene is Level && RenderStrength > 0) {
                Level level = scene as Level;

                Entity e = level.Entities.FindFirst<Player>();

                if (e == null) {
                    orig(self, scene);
                    return;
                }

                float scale = Math.Min(Engine.ViewWidth / 320.0f, Engine.ViewHeight / 180.0f);

                Vector2 p = e.Position;

                Matrix m = Matrix.CreateTranslation(p.X - 32, p.Y - 32, 0) * level.Camera.Matrix * Matrix.CreateScale(scale);

                if (RenderStrength > 0.1) {

                    glitchEffect.Parameters["time"].SetValue(scene.RawTimeActive);
                    glitchEffect.Parameters["strength"].SetValue(1);

                    Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, glitchEffect, m);

                } else {

                    ColorGrade.Set(GFX.ColorGrades["isagrabbag/rewind_flash"]);

                    Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, ColorGrade.Effect, m);

                }

                Draw.SpriteBatch.Draw(playerTarget, Vector2.Zero, Color.White);

                Draw.SpriteBatch.End();
            }

            orig(self, scene);
        }

        private static bool PlayerCollider_Check(On.Celeste.PlayerCollider.orig_Check orig, PlayerCollider self, Player player) {
            return !Rewinding && orig(self, player);
        }

        private static void Level_OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
            ClearRewindBuffer();
            Rewinding = false;
            RenderStrength = 0;
        }

        private static void Player_Update(On.Celeste.Player.orig_Update orig, Player self) {
            if (Rewinding) {
                Engine.TimeRate = 1;
                orig(self);
                Engine.TimeRate = 0;
            } else {
                orig(self);
                states[currentFrame] = new CharacterState(self);
                currentFrame++;
                currentFrame %= states.Length;

                if (maxRewind < states.Length) {
                    maxRewind++;
                }
            }
        }

        private void OnPlayer(Player player) {
            if (Rewinding) {
                return;
            }

            Coroutine co = new(RewindRoutine()) {
                UseRawDeltaTime = true
            };
            Audio.Play("event:/new_content/game/10_farewell/pinkdiamond_touch", Position);

            Add(co);
        }

        private IEnumerator RewindRoutine() {

            Player player = Scene.Tracker.GetEntity<Player>();
            Rewinding = true;
            player.Collidable = false;

            Celeste.Freeze(0.05f);
            Collidable = false;
            yield return null;

            visuals.Visible = false;
            if (!oneUse) {
                outline.Visible = true;
            }

            level.Shake();

            player.StateMachine.State = Player.StDummy;
            player.ForceCameraUpdate = true;
            player.DummyGravity = false;

            Engine.TimeRate = 0;

            float f;
            for (f = 0; f < 0.5f; f += Engine.RawDeltaTime) {

                RenderStrength = f * 2;

                if (Input.MenuConfirm.Pressed) {
                    Input.MenuConfirm.ConsumePress();
                    break;
                }

                yield return null;
            }

            RenderStrength = 1;

            int stateLen = states.Length;
            int lastFrame = currentFrame;

            int unpausedState = (currentFrame + 1) % stateLen;
            bool hitPause = false;

            const float rewindLength = 2;

            for (f = 0; f < rewindLength + .15f; f += Engine.RawDeltaTime) {

                if (f < rewindLength && !hitPause) {

                    float sampledCurve = 1 - Ease.SineInOut(f / 2);

                    int offset = (int)((stateLen - 1) * sampledCurve);
                    int newFrame = (currentFrame + offset) % stateLen;

                    while (lastFrame != newFrame) {

                        lastFrame = (lastFrame + stateLen - 1) % stateLen;

                        if (lastFrame > currentFrame && maxRewind < stateLen) {
                            unpausedState = 0;
                            break;
                        }

                        states[lastFrame].SetOnPlayer(player);

                        if (level.CollideCheck<UnpauseCrystal>(player.Collider.Bounds)) {
                            unpausedState = lastFrame;
                            hitPause = true;
                            f = rewindLength - 0.15f;
                            break;
                        }
                    }
                }

                if (f >= rewindLength - 0.15f) {
                    RenderStrength = Calc.ClampedMap(f, rewindLength - 0.15f, rewindLength + 0.15f, 1, 0);// 1 - ((f - rewindLength) / .15f);
                }

                Vector2 position = level.Camera.Position;
                Vector2 cameraTarget = player.CameraTarget;

                level.Camera.Position = position + ((cameraTarget - position) * (1f - (float)Math.Pow(0.01f, 0.1)));

                yield return null;
            }

            RenderStrength = 0;
            states[unpausedState].SetOnPlayerFinal(player);

            yield return 0.1f;

            player.StateMachine.State = Player.StNormal;
            Engine.TimeRate = 1;

            Rewinding = false;

            player.Collidable = true;

            maxRewind = 0;

            ClearRewindBuffer();
            if (oneUse) {
                RemoveSelf();
            } else {
                Add(new Coroutine(RespawnRoutine()));
            }
        }

        private IEnumerator RespawnRoutine() {

            yield return 1.5f;

            visuals.Visible = true;
            Collidable = true;
            Audio.Play("event:/game/general/diamond_return", Position);
        }
    }

    [CustomEntity("isaBag/pauseCrystal")]
    [Tracked(false)]
    public class UnpauseCrystal : Entity {
        private readonly Sprite visuals;

        public UnpauseCrystal(Vector2 position) : base(position) {
            visuals = GrabBagModule.sprites.Create("pause_crystal");
            Collider = new Hitbox(12, 12, -6, -6);
            Add(visuals);
            Depth = 1500;
        }

        public UnpauseCrystal(EntityData data, Vector2 offset)
            : this(data.Position + offset) {
        }
    }

    internal struct CharacterState {
        public Vector2 pos, scale;
        public Facings facing;
        public int dashCount;
        public Color hairColor;
        public Vector2[] hairPosition;
        public MTexture texture;
        public Vector2 velocity;
        public bool inverted;

        public CharacterState(Player player) {
            pos = player.Position;
            scale = player.Sprite.Scale;
            facing = player.Facing;
            texture = player.Sprite.Texture;
            dashCount = player.Dashes;
            velocity = player.Speed;
            PlayerHair hair = player.Hair;
            hairColor = hair.Color;
            hairPosition = new Vector2[10];
            inverted = GravityHelperImports.IsPlayerInverted();

            for (int i = 0; i < Math.Min(hair.Nodes.Count, hairPosition.Length); ++i) {
                hairPosition[i] = hair.Nodes[i];
            }
        }

        public void SetOnPlayer(Player player) {
            player.NaiveMove(pos - player.Position);
            player.Sprite.Scale = scale;
            player.Facing = facing;
            player.Dashes = dashCount;

            player.Sprite.Texture = texture;

            if (GravityHelperImports.HasInterop()) {
                GravityHelperImports.SetPlayerInverted(inverted);
            }

            if (hairPosition == null) {
                return;
            }

            for (int i = 0; i < Math.Min(player.Hair.Nodes.Count, hairPosition.Length); ++i) {
                player.Hair.Nodes[i] = hairPosition[i];
            }
        }
        public void SetOnPlayerFinal(Player player) {
            SetOnPlayer(player);
            player.Speed = velocity;
        }
    }
}
