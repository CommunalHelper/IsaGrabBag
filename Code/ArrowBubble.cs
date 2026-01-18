using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.IsaGrabBag {
    [CustomEntity("isaBag/arrowBubble")]
    public class ArrowBubble : Booster {
        public Vector2 gravityDirection;
        private readonly Sprite sprite;
        private static Vector2 GravityDir;

        public ArrowBubble(EntityData data, Vector2 offset)
            : base(data.Position + offset, true) {
            string dir = data.Attr("direction", "down");

            Add(new PlayerCollider(OnPlayer));

            switch (dir) {
                default:
                    dir = "down";
                    gravityDirection = Vector2.UnitY;
                    break;
                case "up":
                    gravityDirection = -Vector2.UnitY;
                    break;
                case "left":
                    gravityDirection = -Vector2.UnitX;
                    break;
                case "right":
                    gravityDirection = Vector2.UnitX;
                    break;
            }

            DynamicData baseData = new(typeof(Booster), this);
            Remove(Get<Sprite>());
            Add(sprite = GrabBagModule.sprites.Create($"booster_{dir}"));
            baseData.Set("sprite", sprite);

        }

        public static void Load() {
            On.Celeste.Player.RedDashEnd += Player_RedDashEnd;
            On.Celeste.Player.RedDashUpdate += Player_RedDashUpdate;
        }

        public static void Unload() {
            On.Celeste.Player.RedDashEnd -= Player_RedDashEnd;
            On.Celeste.Player.RedDashUpdate -= Player_RedDashUpdate;
        }

        private static void Player_RedDashEnd(On.Celeste.Player.orig_RedDashEnd orig, Player self) {
            orig(self);
            if (GravityDir != Vector2.Zero) {
                GravityDir = Vector2.Zero;
                self.UseRefill(twoDashes: false);
            }
        }

        private static int Player_RedDashUpdate(On.Celeste.Player.orig_RedDashUpdate orig, Player self) {
            if (self.CanDash && self.LastBooster is ArrowBubble arrowBubble) {
                DynamicData boosterData = DynamicData.For(arrowBubble);
                boosterData.Set("respawnTimer", 1f);
                boosterData.Set("cannotUseTimer", 0f);
            }

            int value = orig(self);
            if (GravityDir != Vector2.Zero) {
                float approachSpeed = 350 * Engine.DeltaTime;

                void changeValue(ref float val, float dir) {
                    val = Calc.Approach(val, (val * dir) >= 0 ? (360 * dir) : 0, approachSpeed);
                }

                if (GravityDir.X != 0) {
                    changeValue(ref self.Speed.X, GravityDir.X);
                }

                if (GravityDir.Y != 0) {
                    changeValue(ref self.Speed.Y, GravityDir.Y * (GravityHelperImports.IsPlayerInverted() ? -1 : 1));
                }

                self.DashDir = self.Speed.SafeNormalize();
            }

            return value;
        }

        private void OnPlayer(Player player) {
            sprite.FlipX = false;
            if (player.StateMachine != Player.StRedDash) {
                GravityDir = gravityDirection;
            }
        }
    }
}
