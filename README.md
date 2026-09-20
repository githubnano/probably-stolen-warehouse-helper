# 仓库助手 / Warehouse Helper

适用于 **Probably Stolen Demo** 的 MelonLoader mod。把仓库里的重复操作交给一个用零件拼出来的小助手。

当前版本：**0.1.36**。使用游戏 **0.46D / IL2CPP / Windows x64**、MelonLoader **0.7.3** 和 .NET **6** 的接口编译。其他游戏版本的兼容性尚未确认。当前版本编译通过，已由玩家进行游戏内测试。

[English](README.en.md) · [更新记录](CHANGELOG.md) · [Releases](https://github.com/githubnano/probably-stolen-warehouse-helper/releases)

## 安装

1. 为游戏安装 MelonLoader，并启动一次游戏，让它生成所需文件。
2. 在 [Releases](https://github.com/githubnano/probably-stolen-warehouse-helper/releases) 下载 `WarehouseHelper.dll`。
3. 退出游戏，将 DLL 放进游戏目录的 `Mods` 文件夹。更新时覆盖旧文件，不要同时保留多份。
4. 重新启动游戏。

仓库只包含本 mod 的源码和自制贴图；DLL 通过 Releases 发布。游戏本体、MelonLoader、Unity/游戏程序集、存档及其他 mod 均需自行准备。

## 制作助手

用螺丝刀对模组提取器（机械臂）进行拆解，得到对应的零件堆，再把材料拖到零件堆上完成改装。

| 型号 | 拆解来源 | 追加材料（默认） | 快捷键 |
| --- | --- | --- | --- |
| 基础 | 基础模组提取器 | 垃圾 ×1、电子元件 ×1 | Alt+1–2 |
| 进阶 | 高级模组提取器 | 废金属 ×2、电子元件 ×4 | Alt+1–9 |

## 使用

- 将单个物品或框选的一批物品拖到助手上，先选择操作，再选择快捷键。
- 支持物品当前提供的使用、装备、激活、切换、打开、卸载操作，以及移动。
- 同一个快捷键可以绑定多个物品；重新绑定会替换该键之前的全部绑定。批量操作会逐件执行，可能消耗多个物品。
- 移动时按快捷键拿起物品，多件会一起移动。左键或再按同一个快捷键放下，右键或 Esc 取消并返回原位。无需先打开来源容器。
- 将已绑定的物品单独拖到助手上，选择“清除绑定”。绑定信息显示在该物品的介绍中，并随存档保存。
- 基础款与进阶款限制的是可设置的快捷键数量；执行已绑定操作时不要求助手在场。Alt+数字不会同时触发原版工具箱。

## 语言和配置

跟随游戏语言，支持简体中文、英语、法语、德语、意大利语、日语、韩语、巴西葡萄牙语、俄语和西班牙语。名称、介绍、绑定菜单、提示、改装进度和配置显示名均已翻译；已有物品会刷新语言。翻译位于 [Locales](src/WarehouseHelper/Locales)。

启动后可在 `UserData/MelonPreferences.cfg` 的 `[WarehouseHelper]` 分类调整材料 ID、材料数量及修饰键。修饰键可选 `None`、`Shift`、`Ctrl`、`Alt`，默认 `Alt`。

## 从源码构建

需要 .NET 6 SDK，以及已运行过 MelonLoader 的本地游戏安装。

```powershell
./build.ps1 -GameDir "D:\Games\Probably Stolen Demo"
```

也可通过 `-DotnetPath` 指定 `dotnet.exe`。脚本只读取本地游戏依赖，生成 `artifacts/WarehouseHelper.dll`，不会自动修改游戏安装。依赖程序集不打包进 mod。

## 许可证

[MIT](LICENSE)。许可证适用于本仓库的 mod 内容；游戏和第三方依赖不随此仓库分发。
