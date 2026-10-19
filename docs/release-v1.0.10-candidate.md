# v1.0.10 初始比例与环境雾修复候选

## 比例
最近用户会话分别记录 hostReferenceHeight=54 / scale=0.8888889 和 hostReferenceHeight=24 / scale=2；同一ICEY角色实例、初始碰撞高度均为2个Unity单位。24/54使画面约缩成正常44.4%。此前修复只保证缓存不受爬梯影响，未防首次载入时Entity.hei还未进入站立状态。

现在从当前Hero原生bodyHei（单位格，正常2.25）计算站立参考高度54像素，不读取瞬时hei。bodyHei尚无效则等待；每个Hero独立缓存，不修改DC实际碰撞体。维持guest固定比例，不按动画、梯子或冲刺碰撞高度反复缩放。carrier.csv增加standing_reference记录bodyHei和瞬时hei。

## 环境雾
ICEY世界绘制期间同时隐藏CamFog并将LightedLayers.fogFactor临时设为0。原生CombineLight的雾混合权重q包含fogScale乘数，因此0消除该层深度雾；不切换enableFog或修改着色器变体，避开旧诊断分支的崩溃路径。finally恢复强度与可见性；Hero使用原生画面。Unity透明导出相机绘制期间暂时关闭RenderSettings.fog并恢复。保留光照、粒子、战斗/蓄力特效及原有环境亮度下限。

## 验证与限制
两个Release编译0警告0错误；8项站立参考及已记录比例回归通过；VerifyOnly及9个原文件SHA256通过。本轮未启动或操作游戏。不同存档启动、换关、爬梯/冲刺后的稳定大小、雾层去除观感与GPU合成需用户实机验证，不宣称已完成游戏验收。

## 使用
正常退出两款游戏后运行本目录Run-Playtest.cmd；自动清理旧中继逻辑保留。请重点检查新会话第一次进入时大小、爬梯/F7切换后的大小、同位置ICEY/Hero背景对比和蓄力特效。旧v1.0.9包及日志保留。
