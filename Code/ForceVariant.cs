﻿using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.IsaGrabBag {
    public enum Variant {
        Hiccups = 0,
        InfiniteStamina = 1,
        Invincible = 2,
        InvisibleMotion = 3,
        LowFriction = 4,
        MirrorMode = 5,
        NoGrabbing = 6,
        PlayAsBadeline = 7,
        SuperDashing = 8,
        ThreeSixtyDashing = 9,
        DashAssist = 10
    }

    public enum VariantState {
        Enabled,
        Disabled,
        EnabledTemporary,
        DisabledTemporary,
        EnabledPermanent,
        DisabledPermanent,
        Toggle,
        SetToDefault
    }

    public static class ForceVariants {
        private static bool?[] Variants { get; set; } = new bool?[] { null, null, null, null, null, null, null, null, null, null, null };

        public static void SaveToSession() {
            IsaSession session = GrabBagModule.Session;
            for (int i = 0; i < Variants.Length; i++) {
                session.Variants_Save[i] = Variants[i];
            }
        }

        public static void GetFromSession() {
            IsaSession session = GrabBagModule.Session;
            for (int i = 0; i < Variants.Length; i++) {
                if (Variants[i] != null && session.Variants_Save[i] == null) {
                    SetVariant(i, VariantState.SetToDefault);
                }

                Variants[i] = session.Variants_Save[i];
            }
        }

        public static void ReinforceSession() {
            IsaSession session = GrabBagModule.Session;
            for (int i = 0; i < 11; i++) {
                Variants[i] = session.Variants_Save[i];
                if (session.Variants_Save[i] == null) {
                    continue;
                }

                SetVariant(i, session.Variants_Save[i].Value ? VariantState.EnabledPermanent : VariantState.DisabledPermanent);
            }
        }

        public static void ResetSession() {
            for (int i = 0; i < Variants.Length; i++) {
                SetVariant(i, VariantState.SetToDefault);
            }
        }

        public static void SetVariant(int variant, VariantState state) {
            SetVariant((Variant)variant, state);
        }

        public static void SetVariant(Variant variant, VariantState state) {
            if (state == VariantState.SetToDefault) {
                Variants[(int)variant] = null;
                SetVariantInGame(variant, GetVariantStatus(variant));
                return;
            }

            bool permanent = state is VariantState.EnabledPermanent or VariantState.DisabledPermanent;
            bool value = state == VariantState.Enabled || state == VariantState.EnabledTemporary || state == VariantState.EnabledPermanent || (state == VariantState.Toggle && !GetVariantStatus(variant));

            SetVariantInGame(variant, value);

            if (permanent) {
                Variants[(int)variant] = value;
            }
        }

        public static bool GetVariantStatus(Variant variant) {
            return variant switch {
                Variant.Hiccups => SaveData.Instance.Assists.Hiccups,
                Variant.InfiniteStamina => SaveData.Instance.Assists.InfiniteStamina,
                Variant.Invincible => SaveData.Instance.Assists.Invincible,
                Variant.InvisibleMotion => SaveData.Instance.Assists.InvisibleMotion,
                Variant.LowFriction => SaveData.Instance.Assists.LowFriction,
                Variant.MirrorMode => SaveData.Instance.Assists.MirrorMode,
                Variant.NoGrabbing => SaveData.Instance.Assists.NoGrabbing,
                Variant.PlayAsBadeline => SaveData.Instance.Assists.PlayAsBadeline,
                Variant.SuperDashing => SaveData.Instance.Assists.SuperDashing,
                Variant.ThreeSixtyDashing => SaveData.Instance.Assists.ThreeSixtyDashing,
                Variant.DashAssist => SaveData.Instance.Assists.DashAssist,
                _ => false,
            };
        }

        internal static void Load() {
            On.Celeste.ChangeRespawnTrigger.OnEnter += OnChangeRespawn;
        }

        internal static void Unload() {
            On.Celeste.ChangeRespawnTrigger.OnEnter -= OnChangeRespawn;
        }

        private static void OnChangeRespawn(On.Celeste.ChangeRespawnTrigger.orig_OnEnter orig, ChangeRespawnTrigger self, Player player) {
            orig(self, player);
            SaveToSession();
        }

        private static void SetVariantInGame(Variant variant, bool value) {
            // Set value in game
            switch (variant) {
                case Variant.Hiccups:
                    SaveData.Instance.Assists.Hiccups = value;
                    break;
                case Variant.InfiniteStamina:
                    SaveData.Instance.Assists.InfiniteStamina = value;
                    break;
                case Variant.Invincible:
                    SaveData.Instance.Assists.Invincible = value;
                    break;
                case Variant.InvisibleMotion:
                    SaveData.Instance.Assists.InvisibleMotion = value;
                    break;
                case Variant.LowFriction:
                    SaveData.Instance.Assists.LowFriction = value;
                    break;
                case Variant.MirrorMode:
                    SaveData.Instance.Assists.MirrorMode = Input.Aim.InvertedX = Input.MoveX.Inverted = value;
                    break;
                case Variant.NoGrabbing:
                    SaveData.Instance.Assists.NoGrabbing = value;
                    break;
                case Variant.PlayAsBadeline:
                    bool originalValue = SaveData.Instance.Assists.PlayAsBadeline;
                    SaveData.Instance.Assists.PlayAsBadeline = value;
                    if (value != originalValue) {
                        // apply the effect immediately
                        Player entity = Engine.Scene.Tracker.GetEntity<Player>();
                        if (entity != null) {
                            PlayerSpriteMode mode = SaveData.Instance.Assists.PlayAsBadeline ? PlayerSpriteMode.MadelineAsBadeline : entity.DefaultSpriteMode;
                            if (entity.Active) {
                                entity.ResetSpriteNextFrame(mode);
                                return;
                            }

                            entity.ResetSprite(mode);
                        }
                    }

                    break;
                case Variant.SuperDashing:
                    SaveData.Instance.Assists.SuperDashing = value;
                    break;
                case Variant.ThreeSixtyDashing:
                    SaveData.Instance.Assists.ThreeSixtyDashing = value;
                    break;
                case Variant.DashAssist:
                    SaveData.Instance.Assists.DashAssist = value;
                    break;
            }
        }
    }

    [CustomEntity("ForceVariantTrigger")]
    public class ForceVariantTrigger : Trigger {
        public Variant variant;
        public VariantState enable;

        private bool previous;

        public ForceVariantTrigger(EntityData data, Vector2 offset) : base(data, offset) {
            variant = data.Enum("variantChange", Variant.Hiccups);
            enable = data.Enum("enableStyle", VariantState.Enabled);

            if (data.Has("enforceValue")) {
                bool enforce = data.Bool("enforceValue");
                if (enable == VariantState.Enabled) {
                    enable = VariantState.EnabledTemporary;
                } else if (enable == VariantState.Disabled) {
                    enable = VariantState.DisabledTemporary;
                } else if (enable == VariantState.EnabledPermanent && !enforce) {
                    enable = VariantState.Enabled;
                } else if (enable == VariantState.DisabledPermanent && !enforce) {
                    enable = VariantState.Disabled;
                }
            }
        }

        public override void OnEnter(Player player) {
            base.OnEnter(player);
            previous = ForceVariants.GetVariantStatus(variant);
            ForceVariants.SetVariant(variant, enable);
        }

        public override void OnLeave(Player player) {
            base.OnLeave(player);

            switch (enable) {
                case VariantState.DisabledTemporary:
                case VariantState.EnabledTemporary:
                    ForceVariants.SetVariant(variant, previous ? VariantState.Enabled : VariantState.Disabled);
                    break;
            }
        }
    }
}
