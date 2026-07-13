# StickyNotes — Windows 桌面便签

将便签钉在 Windows 桌面上，**免疫 Win+D**（显示桌面不会隐藏便签）。支持透明度调节、待办复选框、霞鹜文楷字体、磨砂玻璃效果。

## 功能

- **桌面钉住**: 通过 Win32 WM_WINDOWPOSCHANGING hook 阻止最小化，Win+D 不会隐藏便签
- **磨砂玻璃**: DWM Acrylic 背景模糊，透明度滑块可从完全透明渐变到强磨砂
- **Markdown 复选框**: 行首 ☐/☑ 一键切换，支持删除线（已完成）和批量清除
- **动态排版**: 弹窗滑块调节字体大小 (10-36px) 和行间距 (1.0-2.5x)
- **8 种颜色**: 黄/粉/绿/蓝/紫/橙/白/暗，自动适配前景色
- **锁定模式**: 锁定后禁止编辑和拖动，仍可勾选待办
- **多便签支持**: 新建/删除/拖拽/缩放
- **系统托盘**: 后台运行，托盘图标控制

## 快速开始

### 方式一：便携版（推荐）
下载 `publish/StickyNotes.exe`（约 69 MB），双击运行。无需安装 .NET 运行时。

### 方式二：精简版
下载 `publish-fd/StickyNotes.exe`（约 1.5 MB），需要安装 [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。首次运行时系统会自动提示下载。

### 从源码构建
```bash
dotnet build -c Release
dotnet run --project src/StickyNotes
```

## 使用说明

| 操作 | 方法 |
|------|------|
| 新建便签 | 点击 `+` 按钮 |
| 移动便签 | 拖动顶部空白区域 |
| 调整大小 | 拖动窗口边缘 |
| 切换颜色 | 点击 `🎨` 按钮 |
| 调节透明度 | 底部滑块（0.1 完全透明 ~ 1.0 强磨砂） |
| 字体大小 | 点击 `Aˢ` 弹窗滑块 |
| 行间距 | 点击 `↕ˢ` 弹窗滑块 |
| 创建待办 | 光标在行上，点 `☑` 或按 `Ctrl+D` |
| 切换待办 | 点击 ☐/☑ 字符 |
| 清除已完成 | 点击 `🗑` 按钮 |
| 锁定便签 | 点击 `🔒`，右键菜单可解锁 |
| 删除便签 | 点击 `✕` |

### 待办语法
```
☐ 买菜          → 待办（未完成）
☑ 写代码        → 待办（已完成，带删除线）
普通文本        → 无复选框，纯文本
```
存储格式为 Markdown 兼容的 `- [ ]` / `- [x]`，可跨编辑器使用。

## 注意事项

### Windows 10 兼容性
- **磨砂背景不可用**: Win10 的 DWM 不支持 `ACCENT_ENABLE_ACRYLICBLURBEHIND`，便签背景将显示为纯色半透明，无模糊效果
- **透明度滑块仍可正常使用**: 从透明到不透明渐变生效，只是没有毛玻璃模糊
- **建议**: Win10 用户将透明度调低（0.3-0.5）以获得较好的视觉效果

### Windows 11 兼容性
- 需要 **Build 22000+** 才能启用 DWM 圆角
- 需要 **Build 22621+**（22H2）才能获得最佳 Acrylic 磨砂效果
- 部分精简版/企业版 Windows 可能禁用 DWM 视觉效果，导致背景全透明

### 多显示器
- 便签坐标使用虚拟屏幕坐标系，支持负坐标
- 切换显示器配置后便签位置可能偏移，属于正常现象

### 已知限制
- **TextBox 逐行颜色**: WPF TextBox 不支持逐行文字变色，已完成待办使用 Unicode 删除线 (`̶`) 区分，而非变灰
- **字体渲染**: 霞鹜文楷 (LXGW WenKai) 为可选字体，未安装时自动回退到 Segoe UI Variable
- **高 DPI**: 已启用 PerMonitorV2 模式，但在部分老旧应用中可能存在缩放异常
- **Explorer 重启**: 系统资源管理器重启后便签会自动重新挂载

### 系统要求
- Windows 10 1809+ / Windows 11
- .NET 10.0 Desktop Runtime（仅框架依赖版需要）
- 64 位操作系统

## 技术栈

- WPF + Windows Forms (HWND 互操作)
- .NET 10.0
- Microsoft.Extensions.DependencyInjection
- DWM Composition API (Accent Policy)
- WorkerW 桌面层挂载

## 许可

MIT License
