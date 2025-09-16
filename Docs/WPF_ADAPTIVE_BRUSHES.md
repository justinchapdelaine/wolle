The new \*\*Fluent theme in .NET 9 WPF\*\* actually ships with a fairly large set of adaptive brushes, and they’re grouped by purpose (text, backgrounds, accents, control fills, strokes, etc.).  



From the \[official Fluent theme documentation and resource dictionaries](https://github.com/dotnet/wpf/blob/main/Documentation/docs/using-fluent.md), here’s the \*\*full set of brush keys\*\* you can use that will automatically adapt to `ThemeMode="System"`:



---



\### \*\*Text Brushes\*\*

\- `TextFillColorPrimaryBrush`  

\- `TextFillColorSecondaryBrush`  

\- `TextFillColorTertiaryBrush`  

\- `TextFillColorDisabledBrush`



---



\### \*\*Accent Brushes\*\*

\- `AccentFillColorDefaultBrush`  

\- `AccentFillColorSecondaryBrush`  

\- `AccentFillColorTertiaryBrush`  

\- `AccentFillColorDisabledBrush`  

\- `AccentTextFillColorPrimaryBrush`  

\- `AccentTextFillColorSecondaryBrush`  

\- `AccentTextFillColorTertiaryBrush`  

\- `AccentTextFillColorDisabledBrush`



---



\### \*\*Background Brushes\*\*

\- `SolidBackgroundFillColorBaseBrush`  

\- `SolidBackgroundFillColorSecondaryBrush`  

\- `SolidBackgroundFillColorTertiaryBrush`  

\- `SolidBackgroundFillColorQuarternaryBrush`



---



\### \*\*Control Fill Brushes\*\*

\- `ControlFillColorDefaultBrush`  

\- `ControlFillColorSecondaryBrush`  

\- `ControlFillColorTertiaryBrush`  

\- `ControlFillColorQuarternaryBrush`  

\- `ControlFillColorDisabledBrush`



---



\### \*\*Control Stroke Brushes\*\*

\- `ControlStrokeColorDefaultBrush`  

\- `ControlStrokeColorSecondaryBrush`  

\- `ControlStrokeColorOnAccentBrush`  

\- `ControlStrokeColorForStrongFillWhenOnImageBrush`  

\- `ControlStrokeColorOnAccentSecondaryBrush`  

\- `ControlStrokeColorOnAccentTertiaryBrush`  

\- `ControlStrokeColorOnAccentDisabledBrush`



---



\### \*\*Card \& Layer Brushes\*\*

\- `CardBackgroundFillColorDefaultBrush`  

\- `CardBackgroundFillColorSecondaryBrush`  

\- `CardStrokeColorDefaultBrush`  

\- `CardStrokeColorDefaultSolidBrush`  

\- `LayerFillColorDefaultBrush`  

\- `LayerFillColorAltBrush`



---



\### \*\*Miscellaneous\*\*

\- `FocusStrokeColorOuterBrush`  

\- `FocusStrokeColorInnerBrush`  

\- `TextOnAccentFillColorPrimaryBrush`  

\- `TextOnAccentFillColorSecondaryBrush`  

\- `TextOnAccentFillColorDisabledBrush`



---



💡 \*\*How to use them\*\*  

Anywhere you currently have a hard‑coded color, swap it for:



```xml

Foreground="{DynamicResource TextFillColorPrimaryBrush}"

Background="{DynamicResource SolidBackgroundFillColorBaseBrush}"

```



These will automatically pull the correct light or dark value from the Fluent theme when `ThemeMode="System"` is active.



---
