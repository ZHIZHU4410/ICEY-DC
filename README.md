当前候选版本 **v1.0.10-playtest-candidate**：改用原生bodyHei解决初始高度采样导致缩小；ICEY绘制时关闭CamFog、深度雾及Unity环境雾。保留全视野蓄力斩击和既有修复。编译、比例回归和校验通过，实机效果待用户手测。详见 [版本记录](docs/release-v1.0.10-candidate.md)。下方旧版记录仅保留历史背景。

# ICEY-DC / IceyDCBridge

在 Dead Cells 世界中操作**原生 ICEY 玩家**的双进程桥接试用项目。ICEY 持续运行自己的玩家逻辑；Dead Cells 提供关卡、敌人、相机与交互载体，转发输入并显示 ICEY 当前透明帧。

当前源码版本：**1.0.3-playtest**。移动与多项原生动作已有实机验证，完整战斗、防御和世界交互仍未全部验收；版本号不表示最终目标已经完成。

本仓库发布自研源码、构建脚本和教程。游戏、加载器、游戏 DLL、资源、存档及私人运行日志需留在使用者自己的电脑上。**这里没有预编译下载包；先按下面步骤构建。**

## 构建和开始使用

需要 Windows x64、自己安装的两款游戏、BepInEx 和 DCCM。已测环境固定为：ICEY Unity **5.4.0f3**、BepInEx **5.4.23.2**、Dead Cells **35 / 8b5231c**、DCCM **35.13.8.0**。九个原文件指纹见 [版本基线](config/tested-original-hashes.json)；不匹配会明确报错。

1. 安装 .NET 10 SDK、Visual Studio 2022 Build Tools（C++ x64 与 Windows SDK）；在 DCCM 中安装 MDK。使用 **x64 Native Tools Command Prompt for VS 2022**，再进入 PowerShell。
2. 克隆仓库，复制 `config/paths.example.psd1` 为 `config/paths.local.psd1`，填写两游戏实际目录，以及需要备份的存档目录。详细要求见 [构建教程](docs/BUILD.md)。
3. 执行：

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\New-PlaytestPackage.ps1
   ```

4. 关闭两游戏，进入生成的 `dist/v1.0.3-playtest/`，双击 `Run-Playtest.cmd`。分别进入 ICEY 和 DC 关卡，保持 DC 前台，后台 ICEY 不要最小化。DC 菜单用鼠标选择“游玩 → 继续”。
5. 结束时运行**同一包**的 `Stop-Playtest.cmd`，保留会话日志与备份。

构建不会安装插件或控制游戏。启动脚本会核对版本、备份配置中列出的存档和旧桥接模块，再安装自有模块并启动两游戏。不要直接沿用旧会话标记重开 DC。

## 按键

| 按键 | 功能 |
|---|---|
| WASD / 空格 | 移动、跳跃、二段跳 |
| S + 空格 | 下穿 DC 单向平台，实心地板不下穿 |
| J / 长按 J / I | 普攻、连招 / 蓄力 / 重攻 |
| O / 方向 + O | 冲刺、闪避 |
| L / U | 原生处决、反击确认 / 追击，受目标与窗口限制 |
| Q / E | 剑刃风暴 / 影分身，保留解锁和消耗条件 |
| F / Enter | DC 互动键 / 短按互动备用键，暂切 Hero 后返回 |
| Esc | DC 原生暂停菜单，退出后返回 ICEY |
| F7 | 手动切换原生 Hero / ICEY |
| F8 | 测试回血：无消耗回满 ICEY 原生生命，不复活 |

ICEY 实际配置会写入 `native_keymap.csv`；DC 互动键从游戏绑定读取。右上角显示原生生命/护盾，冲刺次数条在**整个屏幕底部中间**。[完整教程与卸载](docs/USAGE.md)

## 当前实现和边界

- 原生移动、动作输入、真实 1024×1024 ARGB32 帧、攻击特效、HP/护盾和冲刺条均通过现有桥接链路；ICEY 负责玩家动作，DC 保留世界身份。
- 本轮 F8 实测原生 HP **83/250 → 250/250**，DC 载体对应 **96 → 289**，原生能量 10 保持。ICEY 模式关闭整屏装饰性 CamFog，深度雾、灯光和实际烟雾/特效保留。
- 48 条无录屏输入全部消费并独立呈现，软件输入到 present 的 p50/p95/p99 为 **40.86 / 47.95 / 60.72ms**。这是单机小样本，不能称为显示器光子延迟或一帧验收。[验证与失败记录](docs/VALIDATION.md)
- 完整战斗/防御/QTE、精英与 Boss、动态机关、完整换关、世界慢动作同步和复杂场景延迟仍未全部验收。F8 是测试辅助，尚未接通 DC 药瓶回血经济。[已知限制](docs/KNOWN_LIMITATIONS.md)

## 源码分工

| 目录 | 职责 |
|---|---|
| `src/icey` | BepInEx 插件、原生输入边界、场景隔离、镜像碰撞、原生战斗事件和采集 |
| `src/dc` | DCCM 模块、玩家载体旁路、地形/实体输出、帧显示、交互与测试回血 |
| `src/shared` | 协议连接、共享帧邮箱、会话定位 |
| `src/native` | D3D11 CPU 读回；玩家帧、原生 HUD 和冲刺条三个模块 |
| `src/relay.py` | 仅本机 `127.0.0.1:42250` 的消息中继 |
| `scripts` | 构建、完整试用包生成、启动与归档 |

历史工程名 `FullPlayer050` 和 DLL 名称保留兼容性，源码实际对应 1.0.3。[架构](docs/ARCHITECTURE.md) · [版本记录](CHANGELOG.md) · [鸣谢与许可](NOTICE.md)

项目由 ZHIZHU4410 与 OpenAI Codex 协作开发、测试和整理；游戏与加载器权利属于各自权利人。尚未选择开源许可证，详见 [LICENSE.md](LICENSE.md)。
