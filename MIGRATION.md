# Migration Guide

This guide helps you transition from the old "Chain File Editor" to the new "Chain Manager" structure.

## Key Changes

### Project Structure
- **Old**: Single project with mixed concerns
- **New**: Multi-project solution with clean separation

### Naming Conventions
- `ChainFile` → `ChainConfiguration`
- `ProjectConfig` → `ProjectConfiguration`
- `ChainMode` → `ProjectMode`
- `ChainFileService` → `ChainConfigurationService`
- `IChainFileService` → `IChainConfigurationService`

### Namespace Changes
- `ChainFileEditor.*` → `ChainManager.Core.*`
- `ChainFileEditor.UI.*` → `ChainManager.Desktop.*`

### UI Framework
- **Old**: Windows Forms
- **New**: WPF with MVVM pattern

## Code Migration

### Service Usage
```csharp
// Old
var service = new ChainFileService();
var chainFile = service.LoadChainFile(path);

// New
var service = new ChainConfigurationService();
var chainConfig = service.LoadChainFile(path);
```

### Model Access
```csharp
// Old
chainFile.Projects["framework"].Mode = "source";

// New
chainConfig.Projects["framework"].Mode = "source";
```

### Validation
```csharp
// Old
var isValid = service.ValidateChainFile(chainFile);

// New
var isValid = service.ValidateChainFile(chainConfig, out var errors);
```

## Benefits of New Structure

1. **Better Maintainability**: Clear separation of concerns
2. **Modern UI**: WPF provides better user experience
3. **Testability**: Interfaces and dependency injection ready
4. **Extensibility**: Easy to add new features and UI frameworks
5. **Performance**: Optimized code with modern C# patterns

## Backward Compatibility

The new system maintains full compatibility with existing chain files. No changes to file formats or data structures are required.

## Migration Steps

1. **Backup**: Save your current chain files
2. **Build**: Compile the new solution
3. **Test**: Verify functionality with existing files
4. **Deploy**: Replace old executable with new applications
5. **Update**: Update any scripts or shortcuts

## Support

The old Windows Forms interface functionality is fully preserved in the new WPF interface with additional improvements:

- Better data binding
- Improved validation feedback
- Modern visual design
- Enhanced keyboard navigation
- Better error handling