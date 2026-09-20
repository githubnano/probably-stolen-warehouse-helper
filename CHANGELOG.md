# Changelog

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
