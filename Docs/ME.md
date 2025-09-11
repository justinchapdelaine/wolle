Got it — let’s strip this down to the “AI‑readable” logic of why your `ThemeMode="System"` only changes the scrollbar and how to fix it \*\*without\*\* explicitly merging the Fluent dictionary.



---



\## \*\*Problem Model\*\*

1\. \*\*ThemeMode works by swapping theme resource values\*\* (brushes, colors, styles) at runtime.

2\. \*\*Only controls that use `DynamicResource` lookups into those theme resources will change\*\* when the OS theme changes.

3\. \*\*Your XAML hard‑codes colors\*\* (`White`, `Black`, `LightGray`, `Gray`, `Blue`), so those elements are locked to fixed values and ignore theme changes.

4\. Scrollbars change because their default style uses theme resources internally.



---



\## \*\*Cause–Effect Chain\*\*

```

ThemeMode = System

↓

WPF loads Fluent theme resources for Light/Dark

↓

Controls using DynamicResource → values update

Controls using hard-coded colors → values stay fixed

```



---



\## \*\*Solution Model\*\*

\- \*\*Replace hard‑coded colors with theme resource keys\*\*.

\- Use `{DynamicResource <ThemeBrushKey>}` instead of literal colors.

\- Theme brush keys are defined in the Fluent theme and automatically swap between Light/Dark.



---



\## \*\*Mapping Example\*\*

| Hard‑coded value | Theme‑aware replacement |

|------------------|-------------------------|

| `Background="White"` | `Background="{DynamicResource SystemControlBackgroundAltHighBrush}"` |

| `Foreground="Black"` | `Foreground="{DynamicResource TextFillColorPrimaryBrush}"` |

| `Background="LightGray"` | `Background="{DynamicResource SystemControlBackgroundBaseLowBrush}"` |

| `Foreground="Gray"` | `Foreground="{DynamicResource TextFillColorSecondaryBrush}"` |

| `Foreground="Blue"` | `Foreground="{DynamicResource AccentFillColorDefaultBrush}"` |



---



\## \*\*Implementation Rules\*\*

1\. \*\*Always use `DynamicResource`\*\* for theme‑sensitive properties.

2\. \*\*Pick brush keys from the Fluent theme\*\* (e.g., `TextFillColorPrimaryBrush`, `AccentFillColorDefaultBrush`).

3\. \*\*Avoid `AllowsTransparency="True"`\*\* if you want system‑drawn backgrounds — otherwise, you must paint your own background with a theme brush.

4\. \*\*Test in both Light and Dark OS modes\*\* to verify brush swapping.



---



