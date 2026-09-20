using MelonLoader;
using WarehouseHelper;

[assembly: MelonInfo(typeof(WarehouseHelperMod), "WarehouseHelper", "0.1.36", "Kimi")]
[assembly: MelonGame("Questing Goose Studio", "Probably Stolen")]

namespace WarehouseHelper
{
    public class WarehouseHelperMod : MelonMod
    {
        public static WarehouseHelperMod Instance { get; private set; }

        public override void OnInitializeMelon()
        {
            Instance = this;
            Config.Init();
            Sprites.Load();
            HelperLogic.Init();
            Log("WarehouseHelper 0.1.36 loaded");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            I18n.OnSceneChange();
            HelperLogic.OnSceneChange();
        }

        public override void OnUpdate()
        {
            I18n.Tick();
            NativeMenu.Tick();
            HelperLogic.Tick();
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            HelperLogic.OnSceneChange();
        }

        public static void Log(string msg) => Instance?.LoggerInstance.Msg(msg);
        public static void Warn(string msg) => Instance?.LoggerInstance.Warning(msg);
        public static void Err(string msg) => Instance?.LoggerInstance.Error(msg);
    }
}
