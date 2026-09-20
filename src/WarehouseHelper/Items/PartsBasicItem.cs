using Il2Cpp;

namespace WarehouseHelper
{
    /// <summary>基础零件堆(3x3):基础模组提取器拆解产物;1 垃圾 + 1 电子元件改装成基础仓库助手。</summary>
    public class PartsBasicItem : WhItem
    {
        public override string Id => Items.PartsBasicId;

        public override GameItem Create()
        {
            var item = Items.MakePile(Id, I18n.ItemName(Id), I18n.T("parts.basic.description"), 20);
            Conversion.SetupPile(item);
            return item;
        }

        public override void AppendTooltip(RichTextBuilder builder, GameItemElement item)
            => Conversion.AppendProgress(builder, item);
    }
}
