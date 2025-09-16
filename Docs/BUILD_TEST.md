# Build Test

Let's test if the package version fix resolved the compilation errors.

The main issue was:
1. ❌ Wpf.Ui version 3.0.0-preview.7 not found
2. ✅ Fixed by updating to stable version 3.0.3

Now try: `dotnet build` again