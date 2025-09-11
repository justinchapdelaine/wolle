# Context Menu Deregistration & Re-registration

## 🔍 **Problem Identified:**

**Issue:** Right-click > "Untangle the Wolle" on a file does nothing
**Root Cause:** Context menu entry might be incorrect or corrupted

## 🛠️ **Solution: Deregister First, Then Re-register**

### **Step 1: Deregister Current Context Menu**

#### **Method A: Using Built-in Unregister Command (Recommended)**
```bash
# Run this command to deregister
"C:\Path\To\wolle.exe" --unregister
```

#### **Method B: Manual Registry Cleanup**
1. Press `Win + R` and type `regedit`
2. Navigate to: `HKEY_CURRENT_USER\Software\Classes\*\shell\`
3. Right-click on the `wolle` folder and select `Delete`
4. Confirm deletion

#### **Method C: PowerShell Script**
```powershell
# Save as Unregister-Wolle.ps1 and run
Remove-Item -Path "HKCU:\Software\Classes\*\shell\wolle" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Context menu deregistered successfully!" -ForegroundColor Green
```

### **Step 2: Verify Deregistration**

**Check that context menu is gone:**
1. Right-click any file
2. Look for "Untangle the Wolle" - it should NOT appear
3. If it still appears, restart Windows Explorer or reboot

### **Step 3: Re-register Context Menu**

#### **Method A: Run App Without Arguments (Recommended)**
```bash
# Run app without arguments to re-register
"C:\Path\To\wolle.exe"
```

**Expected result:** MessageBox saying "Context menu registered successfully!"

#### **Method B: Manual Registry Verification**
After re-registration, check registry:
1. Open `regedit`
2. Navigate to: `HKEY_CURRENT_USER\Software\Classes\*\shell\wolle`
3. Verify the entry exists and has correct values

### **Step 4: Test Context Menu Execution**

1. **Rebuild app** (important!):
```bash
dotnet build
```

2. **Right-click any file** (.md, .txt, .png, etc.)
3. **Select "Untangle the Wolle"**
4. **Check for new log file** in `C:\Users\User\AppData\Local\Wolle\logs\`

### **📋 Expected Log After Re-registration:**

**Successful context menu execution should show:**
```
[2025-09-09 20:30:00.123] INFO: Application starting
[2025-09-09 20:30:00.125] INFO: Command line args: 1 items    ← KEY: Should be 1, not 0
[2025-09-09 20:30:00.126] INFO: File path received: C:\Path\To\File.md  ← NEW: File path appears
[2025-09-09 20:30:00.127] INFO: Creating MainWindow           ← NEW: Window creation starts
[2025-09-09 20:30:00.128] INFO: MainWindow constructor started  ← NEW: Constructor runs
[2025-09-09 20:30:00.129] INFO: MainWindow constructor completed  ← NEW: Window created
[2025-09-09 20:30:00.130] INFO: Showing MainWindow               ← NEW: Window appears
[2025-09-09 20:30:00.131] INFO: Processing file in MainWindow     ← NEW: File processing starts
[2025-09-09 20:30:00.132] INFO: ProcessFile called with: C:\Path\To\File.md  ← NEW: File processing
[2025-09-09 20:30:00.133] INFO: ShowLoading called - showing loading panel  ← NEW: Progress indicator
```

## 🔍 **Why This Should Fix It:**

### **Potential Issues with Current Registration:**

#### **Issue 1: Incorrect Executable Path**
- **Problem:** Registry points to wrong `.exe` location
- **Solution:** Re-registration sets correct path

#### **Issue 2: Corrupted Registry Entry**
- **Problem:** Registry entry is malformed or corrupted
- **Solution:** Clean delete and recreate

#### **Issue 3: Outdated Registration**
- **Problem:** Registry entry from old build
- **Solution:** Re-register with current executable

#### **Issue 4: Command Syntax Error**
- **Problem:** Command construction in registry is wrong
- **Solution:** Re-registration uses correct syntax

## 🎯 **Complete Fix Procedure:**

### **Phase 1: Deregister**
```bash
# Step 1: Deregister
"C:\Path\To\wolle.exe" --unregister

# Step 2: Verify context menu is gone (right-click any file)
# Should NOT see "Untangle the Wolle"
```

### **Phase 2: Rebuild & Re-register**
```bash
# Step 3: Rebuild with latest fixes
dotnet build

# Step 4: Re-register context menu
"C:\Path\To\wolle.exe"

# Expected: "Context menu registered successfully!" message
```

### **Phase 3: Test**
```bash
# Step 5: Test context menu
# Right-click any file → "Untangle the Wolle"

# Step 6: Check logs
# Look in C:\Users\User\AppData\Local\Wolle\logs\ for new log file
# Should show "Command line args: 1 items"
```

## 🚀 **Let's Do It:**

### **Start with Deregistration:**
```bash
# Run this first:
"C:\Path\To\wolle.exe" --unregister
```

### **Then Let Me Know:**
1. **Did deregistration work?** (MessageBox confirmation)
2. **Is context menu gone?** (Right-click any file to verify)
3. **Ready to re-register?** (I'll guide you through the rest)

**Deregistering first is the right approach - it will clean up any potential issues with the current registration!** 🎯