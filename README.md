# Vampire Crawlers Rumble

为 Windows Steam 版《Vampire Crawlers》增加 DualSense 等 Unity Input System 手柄的事件震动。本项目是独立的 BepInEx 6 IL2CPP 插件；不修改游戏资源和存档。

## 0.2.5 的反馈

| 场景 | 反馈 | 游戏事件依据 |
| --- | --- | --- |
| 村庄（每局返回的基地）拨动选择、确认建筑 | 两种短促轻震 | `TownViewController` 的选择和确认方法 |
| 地牢撞墙、撞到可破坏奖励物 | 一次强震 | 地牢撞击动画与破坏物命中结果 |
| 武器或卡牌效果动画 | 动画存活期间持续变化；命中时加重 | `Weapon.OnSpawned`、`OnDisable`、命中回调；可取得动画片段长度时作为超时界限 |
| 敌人前冲撞击 | 每个敌人一次 70 ms 强短震；连续攻击按动作顺序间隔至少 115 ms | `EnemyAction.Execute` 的敌人动作顺序与攻击前冲时长；若能观察到前冲转折或特效，则使用对应视觉事件；不监听伤害结算 |
| 敌人法术演出 | 法术动画或特效持续期间柔和连续震动；重叠法术独立计时 | 敌人专用动画片段、多人攻击特效生成事件 |
| 金币、经验水晶散落与归位 | 金币为颗粒状清脆节奏，水晶为较柔和的闪烁节奏 | 金币和水晶的视图生成、到达目标事件 |

**打出卡牌本身不再触发震动。** 武器反馈从效果生成开始，到对象停用或安全超时结束。剑、刀、鞭等使用偏硬的低频冲击和短高频颗粒；魔法、火、水、雷等使用较柔和的高频起伏。识别依据是游戏中的卡牌资源名和武器对象名；未知武器使用中性曲线。多个效果同时出现时取各马达的最大值，避免强度无限叠加。

敌人法术识别依据动画片段或特效预制体的名称（如 `spell`、`magic`、`fireball`、`lightning` 等）。持续时间优先取动画片段或粒子系统时长，并限制在 0.25–3 秒。近战短震从 `EnemyAction.Execute` 监测每个敌人的动作，以游戏配置的攻击前冲时长安排提示；连续动作按顺序排队，并在震动之间留出停顿。若能检测到前冲转折或撞击特效，则使用对应视觉事件。`PlayerModel.ResolveDamage` 只写诊断日志，不触发震动。部分法术资产若未包含识别词，可能只触发短震。攻击动作入口与实际撞击画面之间的精确同步仍需游玩验证。

这里使用 [Unity Input System 的双马达接口](https://github.com/Unity-Technologies/InputSystem/blob/develop/Packages/com.unity.inputsystem/Documentation~/gamepad-haptics.md)，以约 30 次/秒更新左右马达。它能区分节奏、时长和强度，但**不是 DualSense 的 PCM 高级触觉音频**。真正的高级波形需要独立的音频或设备输出路径；[Thief 控制器 Mod](https://github.com/petergeneric/thief-dark-project-controller-mod/blob/master/HOW-IT-WORKS.md)和 [SilksongHaptics](https://github.com/alexkiri/SilksongHaptics)展示了两者的区别。本项目不复制其他项目的波形或游戏资产。

## 安装与升级

若本机已安装 BepInEx，升级时只需在退出游戏后替换 `BepInEx/plugins/VampireCrawlersRumble.dll`，再通过 Steam 启动游戏。

在其他电脑安装：

1. 安装 [BepInEx 6 Unity IL2CPP Windows x64，be.755](https://builds.bepinex.dev/projects/bepinex_be/755/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.755%2B3fab71a.zip)。将压缩包**内部**的 `BepInEx`、`dotnet`、`winhttp.dll`、`doorstop_config.ini` 和 `.doorstop_version` 放到游戏根目录。已有 BepInEx 时不要覆盖。
2. 启动游戏一次，等待生成 `BepInEx/interop`，然后退出。
3. 将发行包中的 `VampireCrawlersRumble.dll` 放入 `BepInEx/plugins/`，覆盖旧版同名 DLL。
4. 通过 Steam 启动游戏。`BepInEx/LogOutput.log` 应包含 `Rumble 0.2.5 loaded` 和 `Gamepad detected`。

从 0.1.0 升级只需替换同名 DLL。旧配置 `Events.CardPlayed`、`EnemyDamaged` 和 `PlayerDamaged` 不再使用；新开关首次启动后会写入同一个 `.cfg` 文件。旧版发行包保留在 `dist/`，可用于回退。

## 调整强度

配置文件：`BepInEx/config/com.gamestar.vampirecrawlers.rumble.cfg`。修改后重启游戏。

| 键 | 默认值 | 作用 |
| --- | --- | --- |
| `General.Enabled` | `true` | 总开关 |
| `General.Strength` | `0.7` | 总强度，0–1 |
| `Events.VillageNavigation` | `true` | 村庄选择与确认 |
| `Events.DungeonBumps` | `true` | 撞墙与撞奖励物 |
| `Events.CardEffects` | `true` | 武器动画及命中 |
| `Events.Rewards` | `true` | 金币和经验水晶 |
| `Events.EnemyAttacks` | `true` | 已识别的敌人法术动画与特效 |
| `Events.PlayerHits` | `true` | 敌人前冲转折点或撞击特效的短震（沿用旧配置键名） |
| `Diagnostics.TestOnStart` | `false` | 检测到手柄后测试一次马达，完成后改回 `false` |
| `Diagnostics.DebugEvents` | `false` | 将触发源写入 BepInEx 日志以便排查同步问题 |

若某类反馈没有出现，将 `DebugEvents = true`，复现一次并查看 `BepInEx/LogOutput.log` 的 `Haptic event:` 行。一次 0.2.4 实战日志记录了 31 次敌人攻击入口和 31 次短震提示，但用户发现连续攻击的后几次不易感知。0.2.5 将这些短震分开；用户在四个以上敌人连续攻击的实战中确认每次都有清楚震动，并与撞击音效基本同步。诊断结束后可把 `DebugEvents` 改回 `false`。游戏更新后方法名可能改变，日志中的补丁错误有助于定位。

## 构建和检查

需要 .NET 8 SDK、已安装且首次运行过的 BepInEx 6 IL2CPP，以及本机游戏生成的 `BepInEx/interop`。仓库不包含游戏文件或加载器二进制。

```powershell
.\build.ps1 -GameDir 'C:\Path\To\Vampire Crawlers'
dotnet run --project .\tests\WaveformSmoke.csproj -c Release
```

构建输出在 `dist/VampireCrawlersRumble.dll`。波形检查不需要游戏文件；完整插件编译必须引用本机游戏程序集。

目前确认过 Windows x64、游戏 Unity `6000.0.62f1`、BepInEx `6.0.0-be.755+3fab71a`：源码编译无警告，波形检查通过，游戏启动日志确认插件加载并识别 `DualSense Wireless Controller`。每种游戏事件的实际触感、时长与音画同步仍需要在游玩时观察和微调。

## 卸载

退出游戏后删除 `BepInEx/plugins/VampireCrawlersRumble.dll`。仅当其他 Mod 不使用 BepInEx 时，才进一步移除加载器文件。插件不修改游戏原始资源或存档。

## 项目文件与许可

- `Plugin.cs`：BepInEx 配置和游戏事件补丁。
- `HapticEngine.cs`：事件混合、时长管理和手柄输出。
- `HapticWaveforms.cs`：可独立检查的双马达曲线。
- `HapticScheduling.cs`：同一帧多个视觉撞击的短间隔调度与前冲转折检测。
- `tests/`：无需游戏文件的波形检查。
- `build.ps1`：使用本机游戏程序集构建发行 DLL。
- `CHANGELOG.md`：版本变更；`LICENSE`：MIT 许可。

参考了 [CyberKomodo.DualSense](https://github.com/CyberKomodo/CyberKomodo.DualSense) 关于 Unity DualSense 接口的实践，以及 [BepInEx IL2CPP 插件文档](https://docs.bepinex.dev/master/articles/dev_guide/plugin_tutorial/2_plugin_start.html)。未引入这些项目的代码或二进制。

AI 使用声明 / AI-assisted development
本插件的代码、文档和测试在 AI 辅助下编写与修改。项目维护者审查了发布内容，并使用 DualSense 手柄进行了游戏内测试。
The code, documentation, and tests were developed with AI assistance. The maintainer reviewed the release and tested the plugin in-game with a DualSense controller.
