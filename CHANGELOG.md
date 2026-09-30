# Changelog

## 0.2.0

- 新增右键菜单:对充电器、带电池槽的机器、走私者暗格点右键,打开助手扩展菜单(原生打开/卸载等动作照常保留)。
- 充电器:右键循环开关"自动换出满电电池"(关/普通/大容量优先)。开启后每天睡醒结算时(供电正常才生效)自动遍历所有充电器,把充满的电池换成没满的,满电电池落到替补电池原来的位置。
- 机器:右键逐台开启"自动换电池",机器每运行一次(白天使用或夜间结算)就检查一次,电池不够再跑一次时自动更换,优先满电电池。开关显示在物品介绍里,随存档保存;找不到电池会提示一次。
- 走私者暗格:一键把所有不在暗格内的违禁品(按等级从高到低)和赃物(按单价从高到低)转移进暗格;格子不够会提示数量并把放不下的物品标红(所在容器也一并标红)。之后可右键"放回原位",物品按记录归位、标红取消(记录随场景切换清除)。
- 新配置:id_recharger、id_smuggler_bays、id_trashcan(换电池时跳过垃圾桶里的电池)、charger_auto_swap(充电器自动换电池模式)。

- Add a right-click menu on rechargers, battery-powered machines, and smuggler bays. Native actions (open, unload, etc.) remain available in the same menu.
- Recharger: right-click to cycle auto battery swap (off / normal / large-capacity first). Each morning after sleeping (only while the power stayed on), every recharger swaps its full batteries for partially charged ones; each full battery lands where its replacement was.
- Machines: per-machine auto battery replacement, checked after every run (manual use or nightly processing), preferring fully charged batteries. The toggle shows in the item description and persists in saves; you are notified once if no battery is available.
- Smuggler bay: one-click transfer of all contraband (highest level first) and stolen goods (highest price first) not already inside a bay. Items that do not fit are reported and marked red, along with the containers holding them. A second click puts everything back and clears the marks (records reset on scene change).
- New settings: id_recharger, id_smuggler_bays, id_trashcan (batteries inside trash cans are ignored), charger_auto_swap (recharger auto swap mode).

## 0.1.36

- 材料齐全后，助手在零件堆原位置生成，保留朝向，不再额外寻找空位。
- 移除空间不足时每两秒创建、销毁助手的后台重试；失败时恢复零件堆和材料进度，再次拖入所需材料可重试且不重复消耗。
- 删除早期助手尺寸迁移、旧说明书清理和旧残次品自动扫描。
- 移除测试用 F10 发材料快捷键。已由玩家进行游戏内测试。

- Assemble helpers in the parts pile's original position and orientation without requiring extra space.
- Remove recurring creation/destruction retries. Failed replacements restore the pile and progress; dragging a required material onto a completed pile retries without consuming it.
- Remove obsolete saved-shape migration, retired-manual cleanup, and legacy broken-item scans.
- Remove the temporary F10 material shortcut. In-game testing performed by the player.

## 0.1.35

首次发布 / Initial release.

- 基础款与进阶款仓库助手，分别支持 2 个和 9 个快捷键。
- 支持单件和批量绑定、批量操作、原版框选式移动及取消归位。
- 支持游戏全部 10 种语言，跟随游戏语言切换。
- DLL 通过 [Releases](https://github.com/githubnano/probably-stolen-warehouse-helper/releases) 发布。

- Basic and advanced helpers with 2 and 9 hotkeys respectively.
- Single-item and group bindings, batch actions, native group movement, and cancellation.
- Localization for all 10 game languages, following the selected game locale.
- Compiled DLL available through Releases.
