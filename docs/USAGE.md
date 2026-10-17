# 使用、退出和排错

1. 按 [BUILD](BUILD.md) 构建完整包，核对 `paths.psd1` 的两游戏路径和存档备份目录。
2. 正常关闭两游戏，双击包内 `Run-Playtest.cmd`。版本不符会显示文件、期望指纹和解决方法，不会静默继续。
3. 在 ICEY 进入可操作玩家的关卡；在 DC 用鼠标选择“游玩 → 继续”，再保持 DC 前台。启动脚本不自动选存档或按战斗键，后台 ICEY 不要最小化。
4. 使用 README 的键位。已有原生技能的窗口、资源和解锁条件继续生效。不能随时处决并不表示按键没有接通。
5. 同一包的 `Stop-Playtest.cmd` 负责正常关闭本次游戏、归档日志、检查九个原文件；不强杀、不自动回滚存档。

## 交互与画面

F 是当前已测 DC 互动键。互动和 Esc 暂停会临时切原生 Hero，待原生菜单、演出和控制锁结束后回 ICEY；需要长按就继续按原生互动键。Enter 是短按备用。特殊流程可 F7 手动保持 Hero 再切回。临时 Hero 期间遵循 DC 世界规则，不要留在敌人旁等待。

S+空格使用原生 ICEY JumpDown 能力下穿镜像的 DC 单向平台；实心地面不关闭。右上角是 ICEY 原生生命/护盾；冲刺条在屏幕底部居中，充满后按原生延迟淡出。

F8 是独立无消耗**测试回血**，只在前台 DC、未暂停、ICEY 模式生效。它调用 ICEY 原生 HPRecover 回满生命；不复活、不补护盾、能量、技能资源或冲刺次数，也不消费 DC 药瓶。

## 日志

- 每次会话在包内 `sessions/<UTC时间戳>/`：帧、输入、实体/世界、回血事件以及插件/存档备份。
- 启动检查在 `logs/`。`backup/save_编号` 与 `save_backup_sources.json` 对应本机配置的备份来源。
- ICEY：`BepInEx/LogOutput.log`；DC：`coremod/logs/log_latest.log` 和 `coremod/core/host/last_error.txt`。
- 报错后保留时间戳、按键、当前角色、关卡、完整会话和新 last_error。旧 last_error 可能仍留在磁盘，需核对时间。不要在旧标记下直接重启 DC，以免覆盖固定名 CSV。

## 常见问题

- **指纹不符**：核对 README 的游戏/加载器版本，使用已测版本或重新验证兼容性；不要改掉期望哈希。
- **MDK 找不到**：安装 DCCM MDK，检查 `coremod/core/mdk/build/build.props`。
- **cl 找不到**：使用 VS 的 x64 Native Tools 命令行再运行 PowerShell。
- **角色不显示/输入不转发**：两边都进关、DC 前台、ICEY 未最小化；看 relay_stderr 和双方 FAIL 日志。
- **仍看到局部烟雾**：关闭的是整屏装饰性 CamFog；DC 的深度雾、真实烟雾和特效仍存在。
- **端口占用**：结束旧会话，检查本机 42250；不要并行启动两套桥接。
- **游戏已崩溃**：Stop 的“进程已不存在”不代表正常退出，结合保留日志判断。

## 卸载或回退

结束会话并关闭两游戏后，按 `state.json` 中的安装目标，从该会话 `backup` 恢复先前桥接模块。原先不存在的自有文件移到自己的归档目录。涉及 `BepInEx/plugins/IceyEarlyPlaytestAction.dll`、ICEY 根目录 `ep4_cpu.dll / ep5_hud.dll / ep6_dash.dll`、`coremod/mods/IceyEarlyPlaytestInput` 及会话标记。不要修改游戏原 DLL，不要自动覆盖存档；需要回滚存档时自行选择正确备份。
