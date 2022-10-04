﻿using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System.Collections.Generic;

namespace Celeste.Mod.IsaGrabBag {
    [Tracked]
    [CustomEntity("isaBag/dreamSpinner", "isaBag/dreamSpinFake")]
    public class DreamSpinner : Entity {
        private static readonly Color debrisColor = Calc.HexToColor("c18a53");

        public bool Fragile;
        public bool Fake;

        private static bool dreamdashEnabled = true;
        internal DreamBlock block;
        private bool hasCollided;

        private readonly int ID;

        internal float rotation;
        internal Color color;
        internal List<Vector2> offsets;

        public DreamSpinner(Vector2 position, bool _useOnce, bool _fake) : base(position) {
            Collider = new ColliderList(new Collider[] {
                new Circle(6f, 0f, 0f),
                new Hitbox(16f, 4f, -8f, -3f)
            });

            Add(new PlayerCollider(OnPlayer));
            Add(new LedgeBlocker());

            Fragile = _useOnce;
            Fake = _fake;
            Depth = -8500;
            Collidable = false;
            Visible = false;

            rotation = Calc.Random.Choose(0, 1, 2, 3) * MathHelper.PiOver2;
            offsets = new List<Vector2>();
        }

        public DreamSpinner(EntityData data, Vector2 offset)
            : this(data.Position + offset, data.Bool("useOnce", false), _fake: data.Name == "isaBag/dreamSpinFake") {
            ID = data.ID;
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            if (!Fake) {
                scene.Add(block = new DreamBlock(Center - new Vector2(8, 8), 16, 16, null, false, false));
                block.Visible = false;

                if (GrabBagModule.GrabBagMeta.RoundDreamSpinner) {
                    block.Collider = new Circle(9f, 8, 8);
                }
            }
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);

            color = Color.Black;
            if (!SceneAs<Level>().Session.Inventory.DreamDash) {
                color = new Color(25, 25, 25);
            } else if (Fragile) {
                color = new Color(30, 22, 10);
            }

            foreach (DreamSpinner spinner in Scene.Tracker.GetEntities<DreamSpinner>()) {
                if (spinner.Fragile == Fragile && spinner.ID > ID && (spinner.Position - Position).LengthSquared() < 576f) {
                    offsets.Add((Position + spinner.Position) / 2f);
                }
            }
        }

        public override void Update() {
            if (Fake) {
                return;
            } else if (!InView()){
                block.Active = false;
                return;
            } else {
                block.Active = true;
            }

            Player player = GrabBagModule.playerInstance;

            foreach (Actor deb in Scene.CollideAll<Actor>(block.Collider.Bounds)) {
                if (deb is CrystalDebris) {
                    deb.RemoveSelf();
                }
            }

            if (player != null) {
                dreamdashEnabled = player.Inventory.DreamDash;
                if (Fragile) {
                    bool isColliding = block.Collidable && player.Collider.Collide(block);

                    if (!isColliding && hasCollided) {
                        RemoveSelf();
                        block.RemoveSelf();

                        Audio.Play(SFX.game_06_fall_spike_smash, Position);
                        CrystalDebris.Burst(Center, debrisColor, false, 4);
                        return;
                    }

                    hasCollided = isColliding;
                }

                if ((player.DashAttacking || player.StateMachine.State == Player.StDreamDash) && dreamdashEnabled) {
                    block.Collidable = true;
                    Collidable = false;
                } else {
                    block.Collidable = false;
                    Collidable = true;

                }
            }
        }

        public bool InView() {
            Camera camera = (Scene as Level).Camera;
            return X > camera.X - 16f && Y > camera.Y - 16f && X < camera.X + 320f + 16f && Y < camera.Y + 180f + 16f;
        }

        private void OnPlayer(Player player) {
            player.Die((player.Center - Center).SafeNormalize());
        }
    }

    public class DreamSpinnerRenderer : Entity {
        private const int ParticleCount = 700;

        private readonly MTexture[] particleTextures;
        private DreamParticle[] particles;

        private readonly Image fgSpinnerImage;
        private readonly Image bgSpinnerImage;

        private bool dreamDashEnabled;
        private bool hasDreamSpinners;

        private VirtualRenderTarget particlesTarget;

        private float animTimer = 0f;

        public static BlendState DestinationAlphaBlend = new() {
            ColorSourceBlend = Blend.DestinationAlpha,
            ColorDestinationBlend = Blend.InverseSourceAlpha,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One,
            AlphaBlendFunction = BlendFunction.Add
        };

        public DreamSpinnerRenderer() {
            Depth = -8500;
            AddTag(Tags.Global | Tags.TransitionUpdate);
            Add(new BeforeRenderHook(BeforeRender));

            fgSpinnerImage = new Image(GFX.Game["isafriend/danger/crystal/fg_dream"]).CenterOrigin();
            bgSpinnerImage = new Image(GFX.Game["isafriend/danger/crystal/bg_dream"]).CenterOrigin();
            particleTextures = new MTexture[] {
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(14, 0, 7, 7),
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(7, 0, 7, 7),
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(0, 0, 7, 7),
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(7, 0, 7, 7)
            };
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);

            dreamDashEnabled = SceneAs<Level>().Session.Inventory.DreamDash;

            Calc.PushRandom(0x12F3);
            particles = new DreamParticle[ParticleCount];
            for (int i = 0; i < particles.Length; i++) {
                particles[i].Position = new Vector2(Calc.Random.NextFloat(320f), Calc.Random.NextFloat(180f));
                particles[i].Layer = Calc.Random.Choose(0, 1, 1, 2, 2, 2);
                particles[i].TimeOffset = Calc.Random.NextFloat();
                particles[i].Color = particles[i].Layer switch {
                    0 => Calc.Random.Choose(Calc.HexToColor("FFEF11"), Calc.HexToColor("FF00D0"), Calc.HexToColor("08a310")),
                    1 => particles[i].Color = Calc.Random.Choose(Calc.HexToColor("5fcde4"), Calc.HexToColor("7fb25e"), Calc.HexToColor("E0564C")),
                    2 => particles[i].Color = Calc.Random.Choose(Calc.HexToColor("5b6ee1"), Calc.HexToColor("CC3B3B"), Calc.HexToColor("7daa64")),
                    _ => Color.LightGray
                };
            }

            Calc.PopRandom();
        }

        public void BeforeRender() {
            animTimer += 6f * Engine.DeltaTime;

            hasDreamSpinners = Scene.Tracker.GetEntity<DreamSpinner>() != null;

            if (!hasDreamSpinners) {
                return;
            }

            dreamDashEnabled = SceneAs<Level>().Session.Inventory.DreamDash;

            Camera camera = (Scene as Level).Camera;

            particlesTarget ??= VirtualContent.CreateRenderTarget("dream-spinner-renderer", 320, 180);

            Engine.Graphics.GraphicsDevice.SetRenderTarget(GameplayBuffers.TempA);
            Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, camera.Matrix);

            foreach (DreamSpinner spinner in Scene.Tracker.GetEntities<DreamSpinner>()) {
                if (!spinner.InView()) {
                    continue;
                }

                fgSpinnerImage.SetRenderPosition(spinner.Position).SetColor(spinner.color).SetRotation(spinner.rotation);
                fgSpinnerImage.Render();
            }

            foreach (DreamSpinner spinner in Scene.Tracker.GetEntities<DreamSpinner>()) {
                if (!spinner.InView()) {
                    continue;
                }

                bgSpinnerImage.SetColor(spinner.color).SetRotation(spinner.rotation);

                spinner.offsets.ForEach(position => {
                    bgSpinnerImage.SetRenderPosition(position);
                    bgSpinnerImage.Render();
                });
            }

            Draw.SpriteBatch.End();

            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, DestinationAlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            for (int i = 0; i < particles.Length; i++) {
                int layer = particles[i].Layer;
                Color color = dreamDashEnabled ? particles[i].Color : Color.LightGray * (0.5f + (layer / 2f * 0.5f));
                MTexture texture = layer switch {
                    0 => particleTextures[3 - (int)(((particles[i].TimeOffset * 4f) + animTimer) % 4f)],
                    1 => particleTextures[1 + (int)(((particles[i].TimeOffset * 2f) + animTimer) % 2f)],
                    _ => particleTextures[2]
                };
                Vector2 vector = particles[i].Position - (camera.Position * (0.7f - (0.25f * layer)));
                Vector2 position = new() {
                    X = Utils.Mod(vector.X, 320f),
                    Y = Utils.Mod(vector.Y, 180f)
                };

                texture.DrawCentered(position, color);
            }

            Draw.SpriteBatch.End();

            Engine.Graphics.GraphicsDevice.SetRenderTarget(particlesTarget);
            Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, camera.Matrix);

            foreach (DreamSpinner spinner in Scene.Tracker.GetEntities<DreamSpinner>()) {
                if (!spinner.InView()) {
                    continue;
                }

                Color borderColor = !dreamDashEnabled ? Color.Gray : spinner.Fragile ? Color.Orange * 0.9f : Color.White;

                fgSpinnerImage.SetRenderPosition(spinner.Position).SetColor(borderColor).SetRotation(spinner.rotation);
                DrawBorder(fgSpinnerImage);

                bgSpinnerImage.SetColor(borderColor).SetRotation(spinner.rotation);
                spinner.offsets.ForEach(position => {
                    bgSpinnerImage.SetRenderPosition(position);
                    DrawBorder(bgSpinnerImage);
                });
            }

            Draw.SpriteBatch.Draw(GameplayBuffers.TempA, (Scene as Level).Camera.Position, Color.White);

            Draw.SpriteBatch.End();
        }

        private void DrawBorder(Image image) {
            Vector2 position = image.RenderPosition;
            image.RenderPosition = position + new Vector2(0f, -1f);
            image.Render();
            image.RenderPosition = position + new Vector2(0f, 1f);
            image.Render();
            image.RenderPosition = position + new Vector2(-1f, 0f);
            image.Render();
            image.RenderPosition = position + new Vector2(1f, 0f);
            image.Render();
        }

        public override void Render() {
            if (hasDreamSpinners) {
                Draw.SpriteBatch.Draw(particlesTarget, (Scene as Level).Camera.Position, Color.White);
            }
        }

        public override void SceneEnd(Scene scene) {
            base.SceneEnd(scene);
            Dispose();
        }

        internal static void Load() {
            On.Celeste.LevelLoader.LoadingThread += LevelLoader_LoadingThread;
        }

        internal static void Unload() {
            On.Celeste.LevelLoader.LoadingThread -= LevelLoader_LoadingThread;
        }

        private static void LevelLoader_LoadingThread(On.Celeste.LevelLoader.orig_LoadingThread orig, LevelLoader self) {
            self.Level.Add(new DreamSpinnerRenderer());
            orig(self);
        }

        private void Dispose() {
            if (particlesTarget?.IsDisposed ?? false) {
                particlesTarget.Dispose();
                particlesTarget = null;
            }
        }

        public struct DreamParticle {
            public Vector2 Position;
            public int Layer;
            public Color Color;
            public float TimeOffset;
        }
    }
}
