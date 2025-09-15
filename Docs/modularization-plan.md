# Incremental Modularization Plan

> **Goal:** Implement modularization step-by-step while maintaining working markdown formatting
> **Baseline:** Commit `b3635c2` - working implementation with 300ms debouncing and clean build

---

## 📋 Overview

This document outlines our incremental approach to modularizing the Wolle application. We'll implement changes step-by-step, testing each change thoroughly before proceeding to the next.

### 🎯 Success Criteria
- [ ] Markdown formatting works correctly (bold, italic, lists, headers)
- [ ] Real-time streaming functions properly with debouncing
- [ ] Build succeeds with 0 warnings and 0 errors
- [ ] No regression in existing functionality
- [ ] Each step is committed and working before proceeding

---

## 🚀 Current Working State (Baseline)

### ✅ Implementation Details
- **Simple MainWindow** with direct AppendResponseText
- **300ms debouncing** with DispatcherTimer
- **MarkdownService** with ApplyStyling method
- **Clean build** with SupportedOSPlatform attributes
- **No over-engineering** - simple and direct approach

### ✅ Files Involved
- `Views/MainWindow.xaml.cs` - Main UI and response handling
- `Services/MarkdownService.cs` - Markdown conversion and styling
- `App.xaml.cs` - Application startup and DI configuration

---

## 📝 Modularization Steps

## Step 1: Extract Timer Management ⏱️
> **Risk Level:** LOW | **Estimated Time:** 30 minutes

### 🎯 Objective
Extract debouncing timer logic into a dedicated service while maintaining same 300ms interval.

### 📋 Tasks
- [x] Create `Services/MarkdownDebounceService.cs`
- [x] Extract `_debounceLock` and `_markdownDebounceTimer` logic
- [x] Implement `IDebounceService` interface
- [x] Update MainWindow to use new service via DI
- [x] Register service in `App.xaml.cs`
- [x] **TEST:** Verify markdown formatting still works
- [x] **TEST:** Verify real-time streaming still works
- [x] **TEST:** Verify build is clean (0 warnings, 0 errors)

### 🔄 Expected Changes
```csharp
// Before (MainWindow)
private readonly object _debounceLock = new object();
private DispatcherTimer? _markdownDebounceTimer;

// After (MainWindow)
private readonly IMarkdownDebounceService _debounceService;

// New Service
public class MarkdownDebounceService : IMarkdownDebounceService
{
    public void DebounceMarkdownConversion(string accumulatedText, Action<string> convertCallback)
    {
        // Timer logic moved here
    }
}
```

---

## Step 2: Extract Response State Management 📊
> **Risk Level:** MEDIUM | **Estimated Time:** 45 minutes

### 🎯 Objective
Extract response visibility and accumulation logic into a dedicated service.

### 📋 Tasks
- [x] Create `Services/ResponseStateService.cs`
- [x] Extract `_accumulatedResponseText` and visibility logic
- [x] Implement `IResponseStateService` interface
- [x] Update MainWindow to use new service
- [x] Register service in `App.xaml.cs`
- [x] **TEST:** Verify response state transitions work
- [x] **TEST:** Verify markdown formatting still works
- [x] **TEST:** Verify build is clean

### 🔄 Expected Changes
```csharp
// Before (MainWindow)
private string _accumulatedResponseText = "";

// After (MainWindow)
private readonly IResponseStateService _responseStateService;

// New Service
public class ResponseStateService : IResponseStateService
{
    public void AccumulateText(string text) { /* ... */ }
    public void ResetAccumulatedText() { /* ... */ }
    public string GetAccumulatedText() { /* ... */ }
}
```

---

## Step 3: Extract Markdown Conversion 📝
> **Risk Level:** LOW | **Estimated Time:** 30 minutes

### 🎯 Objective
Extract markdown conversion logic into a dedicated service.

### 📋 Tasks
- [x] Create `Services/MarkdownConversionService.cs`
- [x] Extract conversion logic from MarkdownService
- [x] Keep ApplyStyling method in MarkdownService
- [x] Implement `IMarkdownConversionService` interface
- [x] Update MarkdownService to use new service
- [x] Register service in `App.xaml.cs`
- [x] **TEST:** Verify markdown conversion still works
- [x] **TEST:** Verify all markdown elements render correctly
- [x] **TEST:** Verify build is clean

### 🔄 Expected Changes
```csharp
// Before (MarkdownService)
public FlowDocument ConvertToFlowDocument(string markdown)
{
    // Conversion logic here
}

// After (MarkdownService)
public FlowDocument ConvertToFlowDocument(string markdown)
{
    return _conversionService.ConvertToFlowDocument(markdown, _pipeline);
}

// New Service
public class MarkdownConversionService : IMarkdownConversionService
{
    public FlowDocument ConvertToFlowDocument(string markdown, MarkdownPipeline pipeline)
    {
        // Conversion logic moved here
    }
}
```

---

## Step 4: Extract UI Management 🖥️
> **Risk Level:** MEDIUM | **Estimated Time:** 45 minutes

### 🎯 Objective
Extract UI element management into a dedicated service.

### 📋 Tasks
- [ ] Create `Services/ResponseUIService.cs`
- [ ] Extract FlowDocumentScrollViewer management
- [ ] Implement `IResponseUIService` interface
- [ ] Update MainWindow to use new service
- [ ] Register service in `App.xaml.cs`
- [ ] **TEST:** Verify UI updates work correctly
- [ ] **TEST:** Verify response visibility transitions
- [ ] **TEST:** Verify build is clean

### 🔄 Expected Changes
```csharp
// Before (MainWindow)
ResponseScrollViewer.Document = flowDocument;

// After (MainWindow)
_responseUIService.UpdateDocument(flowDocument);

// New Service
public class ResponseUIService : IResponseUIService
{
    public void UpdateDocument(FlowDocument document) { /* ... */ }
    public void ShowResponseSection() { /* ... */ }
    public void HideResponseSection() { /* ... */ }
}
```

---

## Step 5: Integrate Services 🧩
> **Risk Level:** HIGH | **Estimated Time:** 60 minutes

### 🎯 Objective
Combine all extracted services into a cohesive architecture.

### 📋 Tasks
- [ ] Create `Services/ResponseDisplayCoordinator.cs`
- [ ] Implement orchestration of all services
- [ ] Create single interface for MainWindow
- [ ] Update MainWindow to use coordinator
- [ ] Register all services in `App.xaml.cs`
- [ ] **TEST:** Verify end-to-end functionality
- [ ] **TEST:** Verify markdown formatting works
- [ ] **TEST:** Verify real-time streaming works
- [ ] **TEST:** Verify build is clean
- [ ] **TEST:** Verify no performance regression

### 🔄 Expected Changes
```csharp
// Before (MainWindow)
private readonly IMarkdownDebounceService _debounceService;
private readonly IResponseStateService _responseStateService;
private readonly IMarkdownConversionService _conversionService;
private readonly IResponseUIService _uiService;

// After (MainWindow)
private readonly IResponseDisplayCoordinator _coordinator;

// New Coordinator
public class ResponseDisplayCoordinator : IResponseDisplayCoordinator
{
    public void AppendResponseText(string text)
    {
        // Orchestrates all services
        _stateService.AccumulateText(text);
        _debounceService.Debounce(() => 
        {
            var document = _conversionService.Convert(_stateService.GetAccumulatedText());
            _uiService.UpdateDocument(document);
        });
    }
}
```

---

## 📊 Progress Tracking

| Step | Status | Date | Commit Hash | Notes |
|------|--------|------|-------------|-------|
| **Baseline** | ✅ Complete | 2025-09-14 | `b3635c2` | Working implementation with clean build |
| **Step 1** | ✅ Complete | 2025-09-14 | `692ef81` | Extract Timer Management - working perfectly |
| **Step 2** | ✅ Complete | 2025-09-14 | `33473a8` | Extract Response State Management - working perfectly |
| **Step 3** | ✅ Complete | 2025-09-14 | `53b9817` | Extract Markdown Conversion - working perfectly |
| **Step 4** | ⏳ Not Started | - | - | Extract UI Management |
| **Step 5** | ⏳ Not Started | - | - | Integrate Services |

---

## 🚨 Rollback Strategy

If any step fails, we can immediately rollback to the last working state:

1. **Identify failing step** - Note which step caused issues
2. **Reset to baseline** - `git reset --hard b3635c2`
3. **Document issue** - Add notes to this file about what failed
4. **Adjust plan** - Modify approach based on learnings
5. **Retry** - Start again with adjusted approach

### Quick Rollback Commands
```bash
# Reset to baseline
git reset --hard b3635c2

# Check status
git status

# Verify working
dotnet build
```

---

## 🎯 Success Metrics

### Technical Metrics
- [ ] Build succeeds with 0 warnings, 0 errors
- [ ] All unit tests pass (if implemented)
- [ ] Markdown formatting works for all elements
- [ ] Real-time streaming functions properly
- [ ] Performance meets or exceeds baseline

### Code Quality Metrics
- [ ] Services have clear, single responsibilities
- [ ] Interfaces are well-defined and minimal
- [ ] Dependencies are properly injected
- [ ] Code follows SOLID principles
- [ ] Documentation is comprehensive

### User Experience Metrics
- [ ] Markdown renders correctly and immediately
- [ ] Real-time streaming feels responsive
- [ ] No flickering or visual artifacts
- [ ] Error handling is graceful
- [ ] Performance is consistent

---

## 📝 Notes

### Baseline Verification (2025-09-14)
- ✅ Build: Clean with 0 warnings, 0 errors
- ✅ Markdown: Bold, italic, lists, headers work correctly
- ✅ Streaming: Real-time updates with 300ms debouncing
- ✅ Performance: No lag or stuttering
- ✅ Architecture: Simple and maintainable

### Next Steps
- [ ] Start with Step 1: Extract Timer Management
- [ ] Test thoroughly before proceeding
- [ ] Document any issues or learnings
- [ ] Update progress tracking in this file

---

*Last Updated: 2025-09-14*
*Status: Step 3 Complete - Ready to begin Step 4*