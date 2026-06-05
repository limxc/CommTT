# Hotfix: 程序无法正常启动

## 问题描述
应用程序启动时崩溃，抛出 `System.Exception: 无法找到名为"MaterialDesignFlatButton"的资源`。

## 根因分析
`App.xaml` 的 `MergedDictionaries` 中只引入了 `materialDesign:BundledTheme`（颜色/主题），未引入 `MaterialDesignTheme.Defaults.xaml`（控件样式定义）。

`NavigationView.xaml` 使用了 `Style="{StaticResource MaterialDesignFlatButton}"`，该样式定义在 `MaterialDesignTheme.Defaults.xaml` 中，导致 XAML 解析时找不到资源而崩溃。

## 修复目标
在 `App.xaml` 中补充引入 `MaterialDesignTheme.Defaults.xaml` 资源字典，使所有 MaterialDesign 控件样式可用。
