# 修复方案

## 方案
在 `App.xaml` 的 `ResourceDictionary.MergedDictionaries` 中添加 `MaterialDesignTheme.Defaults.xaml`。

## 修改文件
- `src/CommTT.Shell/App.xaml` — 在 BundledTheme 之后添加 Defaults 资源字典

## 修改内容
```xml
<ResourceDictionary.MergedDictionaries>
    <materialDesign:BundledTheme BaseTheme="Light" PrimaryColor="DeepPurple" SecondaryColor="Lime" />
    <ResourceDictionary Source="pack://application:,,,/MaterialDesignThemes;component/Themes/MaterialDesignTheme.Defaults.xaml" />
</ResourceDictionary.MergedDictionaries>
```
