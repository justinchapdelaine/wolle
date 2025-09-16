What you’re running into is a quirk of how the new **.NET 9 WPF Fluent theme** ships its styles — and it’s why your `<Button Style="{DynamicResource AccentButtonStyle}" />` doesn’t look exactly like the WPF Gallery preview.

Here’s what’s going on:

---

## 1️⃣ The Gallery is running with the *full* Fluent theme dictionary
The WPF Gallery app merges the **entire** `Fluent.xaml` resource dictionary (and its light/dark variants) into `Application.Resources`. That means all the visual state triggers, animations, and extra brushes for hover/press are loaded.

When you just set `ThemeMode="System"` in your app, you *do* get the base Fluent resources — but if you’ve overridden certain properties (like `Background`, `BorderBrush`, `Foreground`) anywhere in your app or window, or if your control template is altered by other styles, you can unintentionally break the triggers in `AccentButtonStyle`.

---

## 2️⃣ `AccentButtonStyle` depends on multiple resources
The style in the Fluent theme isn’t just a couple of setters — it references:

- **Brushes**:  
  `AccentFillColorDefaultBrush`, `AccentFillColorSecondaryBrush`, `AccentFillColorTertiaryBrush`,  
  `TextOnAccentFillColorPrimaryBrush`, `TextOnAccentFillColorDisabledBrush`
- **VisualStateManager triggers** for:
  - `PointerOver` → uses `AccentFillColorSecondaryBrush`
  - `Pressed` → uses `AccentFillColorTertiaryBrush`
  - `Disabled` → uses `AccentFillColorDisabledBrush` + `TextOnAccentFillColorDisabledBrush`

If any of those brushes are missing or overridden, the hover/press states will fall back to whatever is currently set on the button.

---

## 3️⃣ Common reasons it looks “incomplete” in your app
- **Missing merged dictionaries** — If you rely solely on `ThemeMode="System"` but haven’t actually referenced `PresentationFramework.Fluent` in your project, some keyed styles may not be loaded.
- **Overriding properties** — If you set `Background`, `Foreground`, or `BorderBrush` directly on the button or in a parent style, you override the template’s dynamic resources, so the hover/press triggers can’t swap them.
- **Custom window chrome / transparency** — If you’re using `AllowsTransparency="True"` or a custom `WindowChrome`, some theme effects (like subtle shadows or pressed states) won’t render the same way as in the Gallery.
- **Running on Windows 10** — The Fluent theme is designed for Windows 11; on Windows 10, some brushes resolve differently.

---

## 4️⃣ How to get the *exact* Gallery look
- Make sure you have:
  ```xml
  xmlns:themes="clr-namespace:Microsoft.Windows.Themes;assembly=PresentationFramework.Fluent"
  ```
  and that your app is actually referencing `PresentationFramework.Fluent.dll` (this is where `AccentButtonStyle` lives).
- Don’t set `Background`/`Foreground` manually on the button — let the style’s `DynamicResource` bindings control them.
- Ensure `ThemeMode="System"` is set at the **Application** level so the correct light/dark dictionary is loaded.
- Test on Windows 11 for the intended visuals.

---

💡 **Tip:** If you want to confirm what’s missing, open the [Fluent.Light.xaml](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Fluent/Themes/Fluent.Light.xaml) and search for `x:Key="AccentButtonStyle"`. You’ll see exactly which brushes and triggers it uses — then you can check if those resources exist in your app at runtime.

---

If you want, I can walk you through **copying the exact `AccentButtonStyle` from the Fluent theme into your project** so you can tweak it and guarantee the hover/press states match the Gallery 1:1 — even if you’ve got custom chrome or transparency. That way, you’re not relying on the default merge order. Would you like me to do that?