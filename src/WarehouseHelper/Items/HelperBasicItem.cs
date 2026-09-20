using Il2Cpp;

namespace WarehouseHelper
{
    /// <summary>仓库助手(基础,3x3):绑定站,可绑槽位 Alt+1-2。</summary>
    public class HelperBasicItem : WhItem
    {
        public override string Id => Items.HelperBasicId;

        public override GameItem Create()
        {
            var item = Items.MakeHelper(Id, I18n.ItemName(Id), 200);
            HelperLogic.SetupHelper(item);
            return item;
        }

    }
}
