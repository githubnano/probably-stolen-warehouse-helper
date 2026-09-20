using Il2Cpp;

namespace WarehouseHelper
{
    /// <summary>
    /// 一个自定义物品的定义。新增物品:在 Items/ 目录新建一个类继承本类,
    /// 然后在 Items._defs 里登记一行,注册/tooltip 派发全自动。
    /// 行为挂钩(拖拽吸收、绑定等)在 Create 里各自调 Conversion.SetupPile / HelperLogic.SetupHelper。
    /// </summary>
    public abstract class WhItem
    {
        /// <summary>物品 identifier(写存档,必须稳定)。</summary>
        public abstract string Id { get; }

        /// <summary>工厂:创建并完整设置物品(名字/形状/贴图/特征/行为)。</summary>
        public abstract GameItem Create();

        /// <summary>tooltip 末尾追加(GetTooltipBasic 后缀派发到这);不需要就保持空。</summary>
        public virtual void AppendTooltip(RichTextBuilder builder, GameItemElement item) { }
    }
}
