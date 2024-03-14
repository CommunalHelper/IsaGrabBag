using Monocle;
using MonoMod.ModInterop;
using System;

namespace Celeste.Mod.IsaGrabBag {
    public static class ReverseHelperImports {
        [ModImportName("ReverseHelper.DreamBlock")]
        public static class Interop {
            public static Action<Type, Action<Entity>, Action<Entity>> RegisterDreamBlockLike;
            public static Func<Entity, bool> PlayerHasDreamDash;
        }

        public static bool HasInterop() => Interop.PlayerHasDreamDash != null;
        public static bool PlayerHasDreamDash(Entity e) => Interop.PlayerHasDreamDash?.Invoke(e) ?? e.SceneAs<Level>().Session.Inventory.DreamDash;
        public static void RegisterDreamBlockLike(Type targetType, Action<Entity> ActivateNoRoutine, Action<Entity> DeactivateNoRoutine) => Interop.RegisterDreamBlockLike?.Invoke(targetType, ActivateNoRoutine, DeactivateNoRoutine);
    }
}