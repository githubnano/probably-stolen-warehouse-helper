using Il2Cpp;

namespace WarehouseHelper
{
    /// <summary>进阶零件堆(3x3):高级模组提取器拆解产物;2 废金属 + 4 电子元件改装成进阶仓库助手。</summary>
    public class PartsAdvItem : WhItem
    {
        public override string Id => Items.PartsAdvId;

        public override GameItem Create()
        {
            var item = Items.MakePile(Id, I18n.ItemName(Id), I18n.T("parts.advanced.description"), 60);
            Conversion.SetupPile(item);
            return item;
        }

        public override void AppendTooltip(RichTextBuilder builder, GameItemElement item)
            => Conversion.AppendProgress(builder, item);
    }
}
