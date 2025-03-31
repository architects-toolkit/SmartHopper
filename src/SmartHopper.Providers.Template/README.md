# SmartHopper Provider Template

This project serves as a template and guide for creating new AI providers for SmartHopper. Follow these instructions to create your own provider.

## Creating a New Provider

1. **Create a new project**:
   - Copy this template project
   - Rename it to `SmartHopper.Providers.YourProviderName`
   - Update all references to "Template" in the code to your provider name

2. **Implement the provider interface**:
   - Modify `TemplateProvider.cs` to implement your provider's API integration
   - Set `IsEnabled = true` when your provider is ready for use
   - Update the icon in the Resources folder

3. **Customize the settings UI**:
   - Modify `TemplateProviderSettings.cs` to include the settings specific to your provider
   - Update the validation logic to ensure all required settings are provided

4. **Update the factory**:
   - Ensure your provider factory correctly creates instances of your provider and settings classes

5. **Add to solution**:
   - Add your new project to the SmartHopper solution
   - Build the solution to ensure your provider is compiled correctly

## Required Components

Every provider must include:

1. **Provider Class** - Implements `IAIProvider` interface
   - Must have a singleton instance accessible via a static property
   - Must implement all methods required by the interface
   - Should set `IsEnabled` appropriately (false for testing, true for production)

2. **Settings Class** - Implements `IAIProviderSettings` interface
   - Creates and manages the UI for configuring the provider
   - Handles loading and saving settings
   - Validates settings before saving

3. **Factory Class** - Implements `IAIProviderFactory` interface
   - Creates instances of the provider and settings classes
   - This is how the provider is discovered by the ProviderManager

4. **Resources**
   - Include an icon for your provider (16x16 pixels recommended)
   - Configure the Resources.resx file to include your icon

## Provider Discovery

Providers are discovered automatically at runtime by:

1. The ProviderManager searches for DLLs matching the pattern `SmartHopper.Providers.*.dll`
2. It loads each assembly and looks for classes implementing `IAIProviderFactory`
3. It creates instances of the provider and settings using the factory
4. Only providers with `IsEnabled = true` are registered and made available to users

## Best Practices

1. **Error Handling**:
   - Implement robust error handling in your provider
   - Log errors using `Debug.WriteLine` or similar
   - Return meaningful error messages to users

2. **Settings Validation**:
   - Validate all settings before using them
   - Provide clear error messages when validation fails

3. **Documentation**:
   - Document your code thoroughly
   - Include XML documentation comments for all public members

4. **Testing**:
   - Test your provider thoroughly before enabling it
   - Set `IsEnabled = false` during development and testing

## Example Implementation

See the `SmartHopper.Providers.MistralAI` project for a complete example of a provider implementation.
