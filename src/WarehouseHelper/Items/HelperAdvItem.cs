using Il2Cpp;

namespace WarehouseHelper
{
    /// <summary>仓库助手(进阶,3x3):绑定站,可绑槽位 Alt+1-9。</summary>
    public class HelperAdvItem : WhItem
    {
        public override string Id => Items.HelperAdvId;

        public override GameItem Create()
        {
            var item = Items.MakeHelper(Id, I18n.ItemName(Id), 500);
            HelperLogic.SetupHelper(item);
            return item;
        }

    }
}
