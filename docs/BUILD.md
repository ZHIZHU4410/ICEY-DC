# 从源码构建

## 依赖

- Windows x64；本仓库未验收 Linux/Proton、GOG 或其他游戏版本。
- 自己安装的 ICEY、Dead Cells，以及 README 指定的 BepInEx / DCCM 版本。
- .NET 10 SDK；Visual Studio 2022 Build Tools，勾选 C++ x64 与 Windows SDK。
- DCCM 的 MDK 已安装在 `Dead Cells/coremod/core/mdk`。NuGet 依赖固定为 `DeadCellsCoreModding.MDK 1.0.1`，它引用本机 MDK 和游戏代理，不会从仓库取得游戏 DLL。

## 配置

在仓库根目录：

```powershell
Copy-Item .\config\paths.example.psd1 .\config\paths.local.psd1
notepad .\config\paths.local.psd1
```

填写真实游戏目录。`SaveDirectories` 是**本机实际存在**的存档目录列表，启动前逐个备份；不会自动恢复。建议加入 Steam `userdata/<你的账户>/553640`、`userdata/<你的账户>/588650` 和 `Dead Cells/coremod/save` 中存在的目录。空列表表示不备份存档，请自行备份后再测试。`paths.local.psd1` 已忽略，不应提交。

九个固定版本指纹在 `config/tested-original-hashes.json`。构建和启动均会核对；不能为了让检查通过而重新生成游戏原文件基线。版本不符需使用已测版本，或先做新的兼容性验证。

## 编译

打开 **x64 Native Tools Command Prompt for VS 2022**，输入 `powershell -NoProfile` 后运行：

```powershell
.\scripts\Build.ps1
```

它使用配置中的 ICEY Managed / BepInEx 引用编译 net35 插件，使用 DCCM MDK 编译 net10.0 模块，再以 `cl /LD /MT /O2 /EHsc` 编译三个 CPU 读回 DLL。临时 MDK 环境变量仅在此进程设置，并在完成时恢复。输出在 `src/**/bin` 和 `out/native`，不部署到游戏。

## 完整包

```powershell
.\scripts\New-PlaytestPackage.ps1
```

默认下载并校验固定 SHA256 的 Python 3.14.0 x64 embeddable 运行时，保留其 LICENSE。已有相同运行时时可传 `-PythonRuntime <runtime目录>` 复用。无需向系统安装 Python，也不会下载游戏或加载器。

输出 `dist/v1.0.3-playtest/` 与同名 ZIP，包含两个自研插件、三个自研原生模块、relay、教程和便携 Python；包清单校验自有模块，九原文件校验游戏版本。已有输出目录时拒绝覆盖，先将旧包改名归档。

配置文件 `paths.psd1` 可按安装位置调整，不计入模块完整性清单；游戏指纹仍按相对路径验证，不能绕过。首次启动前只检查、不安装：

```powershell
powershell -NoProfile -File .\dist\v1.0.3-playtest\Run-Playtest.ps1 -VerifyOnly
```

本次源码整理会重新编译、生成包并执行 VerifyOnly。可配置路径与公开打包脚本属于发布适配；既有游戏行为证据来自 1.0.3 试用会话，不能当作其他电脑已经验收。
