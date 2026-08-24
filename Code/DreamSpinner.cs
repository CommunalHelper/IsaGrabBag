using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using Monocle;
using System.Collections.Generic;

namespace Celeste.Mod.IsaGrabBag {
    [Tracked] [CustomEntity("isaBag/dreamSpinner", "isaBag/dreamSpinFake")]
    public class DreamSpinner : Entity {
        private static readonly Color debrisColor = Calc.HexToColor("c18a53");

        public bool OneUse;
        public bool Fake;
        public bool ShouldRender;
        public string imagePath;

        internal float rotation;
        internal Color color;
        internal List<Vector2> offsets;

        private readonly int ID;
        private DreamBlock block;
        private bool hasCollided;

        public DreamSpinner(Vector2 position, bool _useOnce, bool _fake, string _imagePath)
            : base(position) {
            Collider = !_fake ? new ColliderList(new Circle(6f), new Hitbox(16f, 4f, -8f, -3f)) : null;
            Add(new PlayerCollider(OnPlayer));
            Add(new LedgeBlocker());

            OneUse = _useOnce;
            Fake = _fake;
            imagePath = _imagePath;
            Depth = -8499; // Update just before our renderer
            Collidable = false;
            Visible = false;
            rotation = Calc.Random.Choose(0, 1, 2, 3) * MathHelper.PiOver2;
            offsets = new List<Vector2>();
        }

        public DreamSpinner(EntityData data, Vector2 offset)
            : this(data.Position + offset, data.Bool("useOnce", false), _fake: data.Name == "isaBag/dreamSpinFake",data.Attr("texturePath", "isafriend/danger/crystal")) {
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
            } else if (OneUse) {
                color = new Color(30, 22, 10);
            }

            foreach (DreamSpinner spinner in Scene.Tracker.GetEntities<DreamSpinner>()) {
                if (spinner.OneUse == OneUse && spinner.ID > ID && (spinner.Position - Position).LengthSquared() < 576f) {
                    offsets.Add((Position + spinner.Position) / 2f);
                }
            }
        }

        public override void Update() {
            if (Fake) {
                return;
            }
            if (!InView()) {
                block.Active = false;
                return;
            }
            block.Active = true;

            base.Update();

            foreach (Actor actor in Scene.CollideAll<Actor>(block.Collider.Bounds)) {
                if (actor is CrystalDebris debris) {
                    debris.RemoveSelf();
                }
            }

            Player player = GrabBagModule.playerInstance;
            if (player != null) {
                if (OneUse) {
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

                if ((player.DashAttacking || player.StateMachine.State == Player.StDreamDash) && player.Inventory.DreamDash) {
                    block.Collidable = true;
                    Collidable = false;
                } else {
                    block.Collidable = false;
                    Collidable = true;
                }
            }
        }

        public bool InView() {
            Camera camera = SceneAs<Level>().Camera;
            return X > camera.X - 16f && Y > camera.Y - 16f && X < camera.X + 320f + 16f && Y < camera.Y + 180f + 16f;
        }

        private void OnPlayer(Player player) {
            player.Die((player.Center - Center).SafeNormalize());
        }
    }

    public class DreamSpinnerRenderer : Entity {
        private const int ParticleCount = 630; // Particle count for a 320x180 Dream Block
        private static readonly Vector2 origin = new Vector2(12f, 12f);
        private static readonly BlendState DreamParticleBlend = new() {
            ColorSourceBlend = Blend.DestinationAlpha,
            ColorDestinationBlend = Blend.InverseSourceAlpha,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One,
            AlphaBlendFunction = BlendFunction.Add
        };

        private readonly MTexture[] particleTextures;
        private readonly Dictionary<string, SpinnerTextures> textureSets = new();

        private class SpinnerTextures {
            public MTexture FgSpinner;
            public MTexture BgSpinner;
            public MTexture FgBorder;
            public MTexture BgBorder;
        }


        private VirtualRenderTarget dreamSpinnerTarget;
        private List<DreamSpinner> spinnersToRender;
        private DreamParticle[] particles;
        private bool dreamDashEnabled;
        private float animTimer;

        public DreamSpinnerRenderer() {
            Depth = -8500;
            AddTag(Tags.Global | Tags.TransitionUpdate);
            Add(new BeforeRenderHook(BeforeRender));

            particleTextures = [
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(14, 0, 7, 7),
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(7, 0, 7, 7),
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(0, 0, 7, 7),
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(7, 0, 7, 7)
            ];


        }
        private SpinnerTextures GetTextures(string imagePath) {
            if (imagePath == "") {
                imagePath = "isafriend/danger/crystal";
            }
            // handle backslashes because people can be dumb sometimes
            imagePath = imagePath.Replace('\\', '/').TrimEnd('/');

            // kind of hate this but its  okay enough
            if (!textureSets.TryGetValue(imagePath, out SpinnerTextures textures)) {
                textures = new SpinnerTextures { FgSpinner = GFX.Game[$"{imagePath}/dreamSpinner_fg"], BgSpinner = GFX.Game[$"{imagePath}/dreamSpinner_bg"],
                    FgBorder = GFX.Game[$"{imagePath}/dreamBorder_fg"], BgBorder = GFX.Game[$"{imagePath}/dreamBorder_bg"] };
                textureSets[imagePath] = textures;
            }

            return textures;
        }


        public override void Awake(Scene scene) {
            base.Awake(scene);

            dreamDashEnabled = SceneAs<Level>().Session.Inventory.DreamDash;

            Calc.PushRandom(0x12F3); // Chosen by Isa ¯\_(ツ)_/¯

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

        public override void Update() {
            base.Update();
            animTimer += 6f * Engine.DeltaTime;
        }

        public override void Render() {
            if (spinnersToRender.Count > 0) {
                Draw.SpriteBatch.Draw(dreamSpinnerTarget, SceneAs<Level>().Camera.Position, Color.White);
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

        private void BeforeRender() {
            spinnersToRender = GetSpinnersToRender();
            if (spinnersToRender.Count <= 0) {
                return;
            }

            Camera camera = SceneAs<Level>().Camera;
            dreamDashEnabled = SceneAs<Level>().Session.Inventory.DreamDash;            

            dreamSpinnerTarget ??= VirtualContent.CreateRenderTarget("dream-spinner-renderer", 320, 180);

            // First we draw our spinner textures and dream particles to a temp buffer
            // We draw the particles with a special BlendState so they will only render over spinner textures
            Engine.Graphics.GraphicsDevice.SetRenderTarget(GameplayBuffers.TempA);
            Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, camera.Matrix);
            DrawSpinnerTextures();
            Draw.SpriteBatch.End();

            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, DreamParticleBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            DrawDreamParticles(camera.Position);
            Draw.SpriteBatch.End();

            // We then switch to our main target, draw our borders, then draw the spinner textures + dream particles on top
            Engine.Graphics.GraphicsDevice.SetRenderTarget(dreamSpinnerTarget);
            Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, camera.Matrix);
            DrawSpinnerBorders();
            Draw.SpriteBatch.Draw(GameplayBuffers.TempA, camera.Position, Color.White);
            Draw.SpriteBatch.End();
        }

        private void DrawSpinnerTextures() {
            foreach (DreamSpinner spinner in spinnersToRender) {
                SpinnerTextures textures = GetTextures(spinner.imagePath);

                textures.FgSpinner.Draw(spinner.Position, origin, spinner.color, Vector2.One, spinner.rotation);

                foreach (Vector2 bgOffset in spinner.offsets) {
                    textures.BgSpinner.Draw(bgOffset, origin, spinner.color);
                }
            }
        }

        private void DrawSpinnerBorders() {
            foreach (DreamSpinner spinner in spinnersToRender) {
                SpinnerTextures textures = GetTextures(spinner.imagePath);

                Color borderColor = !dreamDashEnabled ? Color.Gray : spinner.OneUse ? Color.Orange * 0.9f : Color.White;

                textures.FgBorder.Draw(spinner.Position, origin, borderColor, Vector2.One, spinner.rotation);

                foreach (Vector2 bgOffset in spinner.offsets) {
                    textures.BgBorder.Draw(bgOffset, origin, borderColor);
                }
            }
        }


        private void DrawDreamParticles(Vector2 cameraPos) {
            for (int i = 0; i < particles.Length; i++) {
                int layer = particles[i].Layer;
                Color color = dreamDashEnabled ? particles[i].Color : Color.LightGray * (0.5f + (layer / 2f * 0.5f));

                MTexture texture = layer switch {
                    0 => particleTextures[3 - (int)(((particles[i].TimeOffset * 4f) + animTimer) % 4f)],
                    1 => particleTextures[1 + (int)(((particles[i].TimeOffset * 2f) + animTimer) % 2f)],
                    _ => particleTextures[2]
                };

                Vector2 vector = particles[i].Position - (cameraPos * (0.7f - (0.25f * layer)));
                Vector2 position = new() {
                    X = Utils.Mod(vector.X, 320f),
                    Y = Utils.Mod(vector.Y, 180f)
                };

                texture.DrawCentered(position, color);
            }
        }

        private List<DreamSpinner> GetSpinnersToRender() {
            List<DreamSpinner> spinnersToRender = new();
            foreach (DreamSpinner spinner in Scene.Tracker.GetEntities<DreamSpinner>()) {
                if (spinner.InView()) {
                    spinnersToRender.Add(spinner);
                }
            }

            return spinnersToRender;
        }

        private void Dispose() {
            if (dreamSpinnerTarget?.IsDisposed ?? false) {
                dreamSpinnerTarget.Dispose();
                dreamSpinnerTarget = null;
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