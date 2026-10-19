# v1.0.11 地面蓝色烟雾移除候选

用户指出的贴地蓝绿色烟雾对应 DC 的 LevelDisp.tgSmoke，属于 DP_ROOM_SMOKE 房间装饰层。此前只处理 CamFog、CombineLight 深度雾以及 Unity 相机环境雾，未处理这个独立层。

仅在 ICEY 所在关卡的高清世界 drawRec 期间临时隐藏 tgSmoke，finally 恢复原来的 visible 值。Hero 模式使用原生画面；不删除房间几何或修改游戏资源。没有按颜色或通用 Smoke 名称过滤粒子，因此不触碰攻击、冲刺、蓄力斩击或战斗烟尘。保留v1.0.10站立比例修复及既有功能。

验证：两端Release编译0警告0错误；候选包VerifyOnly及九个游戏原文件SHA256校验通过。本轮未启动或操作游戏，贴地烟雾消失、切回Hero恢复、战斗特效保留仍需用户实机确认。未宣称游戏验收通过。

使用：正常退出两款游戏后运行本目录Run-Playtest.cmd。请在截图所示同一位置比较ICEY/Hero模式的贴地烟雾；检查攻击、冲刺和蓄力光效。旧包与用户测试日志均保留。
