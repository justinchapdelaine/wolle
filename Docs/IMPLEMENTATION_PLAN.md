# Wolle Codebase Implementation Plan

## Overview
This document outlines the implementation plan to fix all critical issues identified in the comprehensive code review. Issues are organized by priority to ensure the most critical security and stability problems are addressed first.

---

## 🔥 High Priority - Security & Stability Issues

### 1.1 HttpClient Management Issues
**Files Affected:** `Services/OllamaService.cs`

- [x] **Replace HttpClient creation with IHttpClientFactory**
  - Add `services.AddHttpClient<OllamaService>();` in App.xaml.cs
  - Inject IHttpClientFactory into OllamaService constructor
  - Replace `new HttpClient()` with `_httpClientFactory.CreateClient()`
  - Remove manual Dispose() calls for HttpClient

- [x] **Configure HttpClient with proper settings**
  - Set timeout and base address in factory configuration
  - Add retry policy with exponential backoff
  - Configure proper headers and user agent

### 1.2 UI Thread Blocking Issues
**Files Affected:** `Views/MainWindow.xaml.cs`

- [x] **Replace Thread.Sleep with Task.Delay**
  - Line 418: Replace `System.Threading.Thread.Sleep(100);` with `await Task.Delay(100, cancellationToken);`
  - Line 439: Replace `System.Threading.Thread.Sleep(100);` with `await Task.Delay(100, cancellationToken);`
  - Make surrounding methods async

- [x] **Add proper async disposal pattern**
  - Implement IAsyncDisposable for MainWindow
  - Use async/await pattern throughout disposal logic
  - Add proper cancellation support

### 1.3 Input Validation Vulnerabilities
**Files Affected:** `Services/ValidationService.cs`

- [x] **Implement comprehensive path validation**
  - Replace current simple string checking with `Path.GetFullPath()`
  - Add validation against allowed base directories
  - Handle path canonicalization and normalization
  - Add support for UNC paths and edge cases

- [x] **Add file size validation**
  - Implement configurable file size limits
  - Add validation before file operations
  - Provide clear error messages for size violations

### 1.4 Command Injection Protection
**Files Affected:** `Services/OllamaService.cs`

- [x] **Enhance argument validation**
  - Use `ProcessStartInfo.ArgumentList` instead of string concatenation
  - Add comprehensive shell injection pattern detection
  - Implement proper argument escaping
  - Add sandbox validation for process execution

- [x] **Add process execution security**
  - Validate executable path before execution
  - Use restricted process execution environment
  - Add process privilege validation

### 1.5 Memory Leak Prevention
**Files Affected:** `Services/OllamaService.cs`

- [x] **Implement bounded collections**
  - Add size limits to `_performanceMetrics` queue (max 1000 items)
  - Add size limits to `_errorHistory` queue (max 500 items)
  - Implement automatic cleanup when limits are reached
  - Add periodic cleanup mechanism

- [x] **Fix Task continuation leaks**
  - Properly cancel and clean up Task.Delay continuations
  - Add CancellationToken support to all async operations
  - Implement proper Task disposal patterns

### 1.6 Resource Management Issues
**Files Affected:** Multiple service files

- [x] **Implement proper async pattern**
  - Replace fire-and-forget Task.Run with proper async/await
  - Add proper exception handling for async operations
  - Implement proper cancellation support

- [x] **Fix Timer operations**
  - Remove timer operations from lock blocks
  - Use lock-free patterns for timer management
  - Add proper timer disposal

---

## ⚡ Medium Priority - Performance & Maintainability Issues

### 2.1 Circular Dependencies
**Files Affected:** `App.xaml.cs`, Multiple service files

- [x] **Remove MainWindow dependency from services**
  - Implement event-based communication pattern
  - Create mediator or event aggregator service
  - Refactor IMessageDisplayService to not depend on MainWindow

- [x] **Implement proper DI patterns**
  - Replace manual service registration with automatic discovery
  - Use proper service lifetime management
  - Implement proper factory patterns where needed

### 2.2 God Class Refactoring
**Files Affected:** `Services/OllamaService.cs`

- [x] **Split OllamaService into focused services**
  - Created `OllamaHttpService` for HTTP operations
  - Created `OllamaProcessService` for process management
  - Created `OllamaPerformanceService` for metrics
  - Created `OllamaFileService` for file operations
  - Created `OllamaTypes.cs` for shared types

- [x] **Implement proper separation of concerns**
  - Each service has single responsibility
  - Use dependency injection between new services
  - Implement proper interface segregation
  - Main OllamaService now acts as orchestrator

### 2.3 Exception Handling Improvements
**Files Affected:** Multiple service files

- [x] **Replace generic catch blocks**
  - Catch specific exception types (HttpRequestException, TaskCanceledException, JsonException, etc.)
  - Add proper logging for all exceptions
  - Implement graceful degradation

- [x] **Implement global exception handling**
  - Add global try-catch in App.xaml.cs for AppDomain, Dispatcher, and TaskScheduler exceptions
  - Create centralized error reporting through ExceptionHandlingService
  - Add user-friendly error messages for different exception types

- [x] **Create centralized exception handling service**
  - Created `IExceptionHandlingService` interface and `ExceptionHandlingService` implementation
  - Added exception history tracking with configurable size limits
  - Added severity levels (Information, Warning, Error, Critical)
  - Added user-friendly message generation for common exception types
  - Added critical exception detection for system-level failures

- [x] **Enhance exception handling in key services**
  - Updated `OllamaHttpService` with specific exception handling and centralized error reporting
  - Updated `OllamaProcessService` with better process startup error handling
  - Updated `MainWindow` constructor with centralized exception handling
  - Added proper cancellation exception handling throughout

- [x] **Add graceful degradation**
  - Network errors show user-friendly messages with retry suggestions
  - Timeout errors provide clear guidance to users
  - File system errors include actionable troubleshooting steps
  - Critical errors trigger appropriate application shutdown procedures

### 2.4 Thread Safety Enhancements
**Files Affected:** Multiple service files

- [x] **Establish lock hierarchy**
  - Define clear lock ordering rules (Level 1 → Level 4)
  - Replace multiple lock objects with single strategy per service
  - Add deadlock detection and prevention through hierarchy

- [x] **Implement thread-safe patterns**
  - Use concurrent collections where appropriate (ConcurrentBag, ConcurrentDictionary)
  - Implement proper async synchronization (SemaphoreSlim instead of lock)
  - Add thread-safe state management with Interlocked operations
  - Eliminate race conditions in FileProcessingService
  - Remove nested lock pattern in EventAggregator
  - Fix mixed lock/async pattern in OllamaPerformanceService

---

## 🔧 Low Priority - Code Quality & Modernization Issues

### 3.1 Modern C# Features Implementation
**Files Affected:** All .cs files

- [x] **Adopt file-scoped namespaces**
  - Convert all files to file-scoped namespace syntax
  - Update using statements accordingly

- [x] **Implement required properties and init-only setters**
  - Use `required` keyword for critical properties
  - Use `init` setters for immutable-after-construction properties

- [x] **Add nullable reference types**
  - Enable nullable reference types project-wide
  - Add proper nullability annotations
  - Fix all nullable warnings

- [x] **Use pattern matching enhancements**
  - Replace if-else with switch expressions where appropriate
  - Use property patterns and type patterns
  - Implement tuple patterns for deconstruction

### 3.2 WPF Best Practices Implementation
**Files Affected:** `Views/MainWindow.xaml.cs`, `Views/MainWindow.xaml`, `ViewModels/MainWindowViewModel.cs`, `ViewModels/RelayCommand.cs`, `App.xaml.cs`

- [x] **Implement proper MVVM pattern**
  - Created `MainWindowViewModel` class with INotifyPropertyChanged
  - Created `RelayCommand` class for command handling
  - Implemented data binding instead of direct UI manipulation
  - Separated UI logic from business logic

- [x] **Replace Dispatcher.Invoke with proper binding**
  - Used data binding for UI updates through ViewModel properties
  - Implemented commands for user interactions (Close, Settings, Save, Cancel)
  - Reduced direct Dispatcher usage throughout the application
  - Added BooleanToVisibilityConverter for proper binding

- [x] **Add proper resource management**
  - Implemented IDisposable for MainWindow with proper cleanup
  - Added proper disposal pattern for CancellationTokenSource and services
  - Enhanced resource cleanup in OnClosed and Dispose methods
  - Added proper nullability annotations and error handling

### 3.3 Code Organization Improvements
**Files Affected:** All files

- [x] **Standardize error handling patterns**
  - Created centralized error handling utilities in ValidationUtilities.cs
  - Updated SettingsService and OllamaFileService to use ExceptionHandlingService consistently
  - Replaced manual try-catch blocks with centralized exception handling
  - Added proper logging patterns throughout services

- [x] **Remove code duplication**
  - Created ValidationUtilities class with common validation methods (ValidateNotNull, ValidateNotNullOrEmpty, ValidateRange, etc.)
  - Created MainWindowServiceFacade to eliminate constructor parameter duplication (reduced from 19 to 4 parameters)
  - Consolidated null checking patterns using ValidationUtilities.ValidateNotNull
  - Removed duplicate service initialization code

- [x] **Improve naming and organization**
  - Implemented consistent naming conventions across all services
  - Organized methods by responsibility within service classes
  - Added proper XML documentation for new utility classes
  - Created focused service interfaces with single responsibilities

### 3.4 Testing Infrastructure (Deferred)
**Note:** Testing infrastructure has been deferred until after MVP completion to avoid maintenance overhead during active development. Will be implemented once core functionality is complete and architecture stabilizes.

- [ ] **Add unit testing framework** (Deferred)
  - Add xUnit or NUnit project
  - Create test classes for all services
  - Implement mock objects for dependencies

- [ ] **Add integration tests** (Deferred)
  - Test end-to-end scenarios
  - Test file processing workflows
  - Test error handling scenarios

- [ ] **Add UI automation tests** (Deferred)
  - Test WPF window interactions
  - Test settings panel functionality
  - Test context menu integration

---

## 📋 Implementation Checklist by Phase

### Phase 1: Critical Security Fixes (Week 1)
- [x] HttpClient management fixes
- [x] Input validation enhancements
- [x] UI thread blocking fixes
- [x] Command injection protection
- [x] Memory leak prevention
- [x] Resource management improvements

### Phase 2: Stability & Performance (Week 2)
- [x] UI thread blocking fixes
- [x] Memory leak prevention
- [x] Resource management improvements
- [x] Thread safety enhancements

### Phase 3: Architecture Refactoring (Week 3-4)
- [x] Circular dependency removal
- [x] God class refactoring
- [x] Proper DI pattern implementation
- [x] Exception handling improvements

### Phase 4: Code Quality & Modernization (Week 5-6)
- [x] Modern C# features adoption
- [x] WPF best practices implementation
- [x] Code organization improvements
- [ ] Testing infrastructure setup (Deferred until MVP completion)

---

## 🎯 Success Criteria

### Security Criteria
- [ ] No path traversal vulnerabilities
- [ ] No command injection possibilities
- [ ] Proper input validation for all user inputs
- [ ] Secure process execution

### Stability Criteria
- [ ] No UI thread blocking
- [ ] No memory leaks
- [ ] Proper resource disposal
- [ ] Graceful error handling

### Performance Criteria
- [ ] Responsive UI under all conditions
- [ ] Efficient resource usage
- [ ] Proper async/await patterns
- [ ] No deadlocks or race conditions

### Maintainability Criteria
- [ ] Clear separation of concerns
- [ ] Proper dependency injection
- [ ] Modern C# patterns
- [ ] Comprehensive test coverage

---

## 📝 Notes

### Implementation Guidelines
1. **Test each fix thoroughly** before moving to the next
2. **Commit frequently** with descriptive messages
3. **Update this document** as progress is made
4. **Communicate blockers** immediately
5. **Follow existing code style** and conventions

### Risk Mitigation
1. **Backup working code** before major refactoring
2. **Implement feature flags** for breaking changes
3. **Add comprehensive logging** for debugging
4. **Create rollback plan** for each major change
5. **Test on multiple environments** before deployment

### Quality Assurance
1. **Run all existing tests** after each change
2. **Perform manual testing** for UI changes
3. **Check for performance regressions**
4. **Verify security fixes** with penetration testing
5. **Get code review** for all major changes

---

*Last Updated: September 17, 2025*
*Status: Phase 3.3 (Code Organization Improvements) - ✅ COMPLETED. Successfully standardized error handling patterns, removed code duplication through ValidationUtilities and MainWindowServiceFacade, and improved naming/organization across all services. All Phase 1-3 items completed. Testing infrastructure deferred until MVP completion.*