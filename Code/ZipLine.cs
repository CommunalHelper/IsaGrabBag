using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Celeste.Mod.IsaGrabBag {
    [CustomEntity("isaBag/zipline")]
    public class ZipLine : Entity {
        public const string NeverUsedZiplineFlag = "IsaGrabBag_NeverUsedZipline";

        private const float ZIP_SPEED = 120f;
        private const float ZIP_ACCEL = 190f;
        private const float ZIP_TURN = 250f;

        private static ZipLine currentGrabbed, lastGrabbed;
        private static float ziplineBuffer;

        private readonly float height;
        private readonly Sprite sprite;
        private readonly bool usesStamina;
        private float speed;
        private bool grabbed;

        private static float targetOffset => GravityHelperImports.IsPlayerInverted() ? -2 : 22;

        public ZipLine(EntityData _data, Vector2 offset)
            : base(_data.Position + offset) {
            LeftEdge = X;
            RightEdge = X;
            foreach (Vector2 node in _data.Nodes) {
                LeftEdge = Math.Min(node.X + offset.X, LeftEdge);
                RightEdge = Math.Max(node.X + offset.X, RightEdge);
            }

            usesStamina = _data.Bool("usesStamina", true);
            height = (_data.Position + offset).Y;
            Collider = new Hitbox(20, 16, -10, 1);
            currentGrabbed = null;
            Depth = -500;

            sprite = GrabBagModule.sprites.Create("zipline");
            sprite.Play("idle");
            sprite.JustifyOrigin(new Vector2(0.5f, 0.25f));
            Add(sprite);

            if (GravityHelperImports.HasInterop()) {
                Add(GravityHelperImports.CreatePlayerGravityListener((player, _, _) => {
                    // if the player has changed gravity while grabbing, we need to move them to the correct position on the same frame
                    if (currentGrabbed == null || !currentGrabbed.grabbed) return;

                    float targetY = currentGrabbed.Y + targetOffset;
                    if (targetY != player.Y) {
                        GravityHelperImports.BeginOverride();
                        player.MoveToY(targetY);
                        GravityHelperImports.EndOverride();
                    }
                }));
            }
        }

        public static int ZipLineState { get; private set; }
        public static bool GrabbingCoroutine => currentGrabbed != null && !currentGrabbed.grabbed;
        public float LeftEdge { get; }
        public float RightEdge { get; }

        public override void Added(Scene scene) {
            base.Added(scene);
            scene.Add(new ZipLineRender(this));
        }

        public override void Update() {
            base.Update();
            Player player = GrabBagModule.playerInstance;

            if (player == null || player.Dead) {
                return;
            }

            if (grabbed) {
                if (Math.Abs(player.Speed.X) > 20) {
                    player.LiftSpeed = player.Speed;
                }

                if (player.CenterX > RightEdge || player.CenterX < LeftEdge) {
                    player.Speed.X = 0;
                }

                player.CenterX = MathHelper.Clamp(player.CenterX, LeftEdge, RightEdge);
                Position.X = player.CenterX;
                Position.Y = height;
            } else {
                if (currentGrabbed == null && player != null && !player.Dead && player.CanUnDuck && Input.GrabCheck && CanGrabZip(this)) {
                    bool isTired = DynamicData.For(player).Get<bool>("IsTired");
                    if (player.CollideCheck(this) && !isTired) {
                        currentGrabbed = this;
                        lastGrabbed = currentGrabbed;
                        player.StateMachine.State = ZipLineState;
                    }
                }

                Position.X += speed * Engine.DeltaTime;
                Position.X = MathHelper.Clamp(Position.X, LeftEdge, RightEdge);
                Position.Y = height;
            }
        }

        public override void Render() {
            if (grabbed) {
                sprite.Visible = true;
                sprite.Play(GrabBagModule.playerInstance.Facing == Facings.Left ? "held_l" : "held_r");
            } else {
                sprite.Visible = false;
            }

            base.Render();
        }

        internal static void Load() {
            Everest.Events.Level.OnLoadLevel += Level_OnLoadLevel;
            Everest.Events.Level.OnTransitionTo += Level_OnTransitionTo;
            On.Celeste.Player.ctor += PlayerInit;
            On.Celeste.Player.Update += OnPlayerUpdate;
            On.Celeste.Player.UpdateSprite += UpdatePlayerVisuals;
        }

        internal static void Unload() {
            Everest.Events.Level.OnLoadLevel -= Level_OnLoadLevel;
            Everest.Events.Level.OnTransitionTo -= Level_OnTransitionTo;
            On.Celeste.Player.ctor -= PlayerInit;
            On.Celeste.Player.Update -= OnPlayerUpdate;
            On.Celeste.Player.UpdateSprite -= UpdatePlayerVisuals;
        }

        private static void Level_OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
            currentGrabbed = lastGrabbed = null;
            if (isFromLoader && (level.Session.StartedFromBeginning || level.Session.RestartedFromGolden) && level.Session.MapData.HasEntity("isaBag/zipline")) {
                level.Session.SetFlag(NeverUsedZiplineFlag, true);
            }
        }

        private static void Level_OnTransitionTo(Level level, LevelData next, Vector2 direction) {
            if (lastGrabbed != null) {
                level.Session.SetFlag(NeverUsedZiplineFlag, false);
            }
        }

        private static void PlayerInit(On.Celeste.Player.orig_ctor orig, Player self, Vector2 position, PlayerSpriteMode spriteMode) {
            orig(self, position, spriteMode);
            ZipLineState = self.StateMachine.AddState(ZipLineUpdate, ZipLineCoroutine, ZipLineBegin, ZipLineEnd);
        }

        private static void OnPlayerUpdate(On.Celeste.Player.orig_Update orig, Player self) {
            orig(self);

            ziplineBuffer = Calc.Approach(ziplineBuffer, 0, Engine.DeltaTime);
            if (!Input.GrabCheck) {
                ziplineBuffer = 0;
            }
        }

        private static void UpdatePlayerVisuals(On.Celeste.Player.orig_UpdateSprite orig, Player self) {
            if (ZipLineState > Player.StIntroThinkForABit && self.StateMachine == ZipLineState) {
                self.Sprite.Scale.X = Calc.Approach(self.Sprite.Scale.X, 1f, 1.75f * Engine.DeltaTime);
                self.Sprite.Scale.Y = Calc.Approach(self.Sprite.Scale.Y, 1f, 1.75f * Engine.DeltaTime);

                if (GrabbingCoroutine) {
                    return;
                }

                self.Sprite.PlayOffset("fallSlow_carry", .5f, false);
                self.Sprite.Rate = 0.0f;
            } else {
                orig(self);
            }
        }

        private static void ZipLineBegin() {
            Player self = GrabBagModule.playerInstance;
            self.Ducking = false;
            self.Speed.Y = 0;
        }

        private static void ZipLineEnd() {
            currentGrabbed.grabbed = false;
            currentGrabbed = null;
            ziplineBuffer = 0.35f;
        }

        private static int ZipLineUpdate() {
            Player self = GrabBagModule.playerInstance;

            if (currentGrabbed == null) {
                return Player.StNormal;
            }

            if (!currentGrabbed.grabbed) {
                return ZipLineState;
            }

            currentGrabbed.speed = self.Speed.X;

            if (Math.Sign(self.LiftSpeed.X) * Math.Sign(self.Speed.X) == -1 || Math.Abs(self.LiftSpeed.X) <= Math.Abs(self.Speed.X)) {
                self.LiftSpeed = self.Speed;
            }

            int moveX = DynamicData.For(self).Get<int>("moveX");
            if (Math.Sign(moveX) == -Math.Sign(self.Speed.X)) {
                self.Speed.X = Calc.Approach(self.Speed.X, moveX * ZIP_SPEED, ZIP_TURN * Engine.DeltaTime);
            } else if (Math.Abs(self.Speed.X) <= ZIP_SPEED || Math.Sign(moveX) != Math.Sign(self.Speed.X)) {
                self.Speed.X = Calc.Approach(self.Speed.X, moveX * ZIP_SPEED, ZIP_ACCEL * Engine.DeltaTime);
            }

            if (!Input.GrabCheck || self.Stamina <= 0) {
                return Player.StNormal;
            }

            if (Input.Jump.Pressed) {
                Input.Jump.ConsumePress();

                if (currentGrabbed.usesStamina) {
                    self.Stamina -= 110f / 8f;
                }

                self.Speed.X *= 0.1f;
                self.Jump(false, true);
                self.LiftSpeed *= 0.4f;

                currentGrabbed.speed = Calc.Approach(currentGrabbed.speed, 0, 20);

                return Player.StNormal;
            }

            if (self.CanDash) {
                return self.StartDash();
            }

            if (currentGrabbed.usesStamina) {
                self.Stamina -= 5 * Engine.DeltaTime;
            }

            return ZipLineState;
        }

        private static IEnumerator ZipLineCoroutine() {
            Player self = GrabBagModule.playerInstance;
            Vector2 speed = self.Speed;
            self.Speed = Vector2.Zero;
            currentGrabbed.speed = 0;

            self.Sprite.Play("pickup");

            self.Play("event:/char/madeline/crystaltheo_lift", null, 0f);

            float originalCurrentGrabbedY = currentGrabbed.Y;
            Vector2 playerLerp = new((self.X + currentGrabbed.X) / 2f, originalCurrentGrabbedY + targetOffset);

            playerLerp.X = MathHelper.Clamp(playerLerp.X, currentGrabbed.LeftEdge, currentGrabbed.RightEdge);
            Vector2 zipLerp = new(playerLerp.X, currentGrabbed.Y);

            Vector2 playerInit = self.Position,
                zipInit = currentGrabbed.Position;

            Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.Linear, 0.07f, true);

            while (tween.Active) {
                tween.Update();

                // adjust the player's target Y in case gravity changes mid-tween
                // note that this may cause us to move quickly near the end of the tween
                playerLerp.Y = originalCurrentGrabbedY + targetOffset;

                MoveEntityTo(self, Vector2.Lerp(playerInit, playerLerp, tween.Percent));
                currentGrabbed.Position = Vector2.Lerp(zipInit, zipLerp, tween.Percent);

                yield return null;
            }

            currentGrabbed.grabbed = true;

            self.Speed = speed;

            playerLerp.Y = originalCurrentGrabbedY + targetOffset;

            MoveEntityTo(self, playerLerp);
            currentGrabbed.Position = zipLerp;

            yield break;
        }

        private static void MoveEntityTo(Actor ent, Vector2 position) {
            GravityHelperImports.BeginOverride();
            ent.MoveToX(position.X);
            ent.MoveToY(position.Y);
            GravityHelperImports.EndOverride();
        }

        private static bool CanGrabZip(ZipLine line) {
            return lastGrabbed != line || ziplineBuffer <= 0;
        }
    }

    public class ZipLineRender : Entity {
        private static readonly Color darkLine = Calc.HexToColor("9292a9");
        private static readonly Color lightLine = Calc.HexToColor("bbc0ce");

        private readonly List<RenderRectangle> renderList = new();
        private readonly ZipLine zipInst;
        private readonly Sprite sprite;

        public ZipLineRender(ZipLine instance) {
            zipInst = instance;

            sprite = GrabBagModule.sprites.Create("zipline");
            sprite.Play("idle");
            sprite.JustifyOrigin(new Vector2(0.5f, 0.25f));
            Add(sprite);

            Depth = 500;
        }

        public override void Render() {
            renderList.Clear();

            Position = zipInst.Position;

            Rectangle tempRect = new((int)zipInst.LeftEdge, (int)zipInst.Y, (int)(zipInst.RightEdge - zipInst.LeftEdge), 1);
            tempRect.Inflate(8, 0);

            renderList.Add(new RenderRectangle(tempRect, darkLine));

            tempRect.Y -= 1;

            renderList.Add(new RenderRectangle(tempRect, lightLine));

            int left = tempRect.Left, right = tempRect.Right;

            renderList.Add(new RenderRectangle(new Rectangle(left - 2, (int)Y - 3, 2, 6), darkLine));
            renderList.Add(new RenderRectangle(new Rectangle(right, (int)Y - 3, 2, 6), darkLine));

            foreach (RenderRectangle rl in renderList) {
                Rectangle r = rl.rect;
                r.Inflate(1, 0);
                Draw.Rect(r, Color.Black);
                r.Inflate(-1, 1);
                Draw.Rect(r, Color.Black);
            }

            foreach (RenderRectangle rl in renderList) {
                Draw.Rect(rl.rect, rl.color);
            }

            base.Render();

        }
    }

    public struct RenderRectangle {
        public Rectangle rect;
        public Color color;

        public RenderRectangle(Rectangle r, Color c) {
            rect = r;
            color = c;
        }
    }
}
