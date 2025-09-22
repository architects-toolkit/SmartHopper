# Fix-SA1513.ps1
# Inserts a blank line after a closing brace '}' when required by StyleCop SA1513.

param(
    [Parameter(Mandatory = $false)]
    [string[]] $Files
)

function ShouldInsertBlankLine($lines, $i) {
    # $i is 0-based index where $lines[$i] contains only a closing brace
    if ($i -lt 0) { return $false }
    $nextIndex = $i + 1
    if ($nextIndex -ge $lines.Count) { return $false } # EOF: nothing to insert after

    $current = $lines[$i]
    $next = $lines[$nextIndex]

    # Already blank next line
    if ($next -match '^\s*$') { return $false }

    # Next line begins with another closing brace → keep braces together
    if ($next -match '^\s*\}') { return $false }

    # Next line begins with else/catch/finally → must stay attached to brace
    if ($next -match '^\s*(else|catch|finally)\b') { return $false }

    # Next line is a single-line comment directly after the brace: keep attached
    if ($next -match '^\s*//') { return $false }

    # XML doc comments should be separated by a blank line after a closing brace per SA1513, so allow insertion
    return $true
}

function ProcessFile($filePath) {
    $original = Get-Content -LiteralPath $filePath -Raw
    $lines = $original -split "`r?`n", 0

    $changed = $false
    $output = New-Object System.Collections.Generic.List[string]
    $count = $lines.Count

    for ($i = 0; $i -lt $count; $i++) {
        $line = $lines[$i]
        $output.Add($line)

        # Match a line with only a closing brace (allow leading/trailing whitespace)
        if ($line -match '^\s*\}\s*$') {
            if (ShouldInsertBlankLine -lines $lines -i $i) {
                $output.Add("") # insert a blank line
                $changed = $true
            }
        }
    }

    if ($changed) {
        $newText = [string]::Join([Environment]::NewLine, $output)
        Set-Content -LiteralPath $filePath -Value $newText -NoNewline
        Write-Host "Fixed SA1513 spacing in: $filePath"
    }
    else {
        Write-Host "No changes needed: $filePath"
    }
}

# If explicit list not provided, restrict to the files from the warning report
if (-not $Files -or $Files.Count -eq 0) {
    $Files = @(
        'src/SmartHopper.Core.Grasshopper/Utils/Put.cs',
        'src/SmartHopper.Infrastructure/AICall/Core/Interactions/AIBody.cs',
        'src/SmartHopper.Infrastructure/AICall/Core/Interactions/AIBodyBuilder.cs',
        'src/SmartHopper.Infrastructure/AICall/Core/Interactions/AIBodyExtensions.cs',
        'src/SmartHopper.Infrastructure/AICall/Core/Interactions/AIInteractionImage.cs',
        'src/SmartHopper.Infrastructure/AICall/Core/Interactions/AIInteractionText.cs',
        'src/SmartHopper.Infrastructure/AICall/Core/Interactions/AIInteractionToolCall.cs',
        'src/SmartHopper.Infrastructure/AICall/Core/Requests/AIRequestBase.cs',
        'src/SmartHopper.Infrastructure/AICall/Core/Requests/AIRequestCall.cs',
        'src/SmartHopper.Infrastructure/AICall/JsonSchemas/JsonSchemaService.cs',
        'src/SmartHopper.Infrastructure/AICall/Policies/PolicyPipeline.cs',
        'src/SmartHopper.Infrastructure/AICall/Policies/Request/RequestTimeoutPolicy.cs',
        'src/SmartHopper.Infrastructure/AICall/Sessions/ConversationSession.cs',
        'src/SmartHopper.Infrastructure/AICall/Tools/AIToolCall.cs',
        'src/SmartHopper.Infrastructure/AICall/Tools/ToolResultEnvelope.cs',
        'src/SmartHopper.Infrastructure/AICall/Validation/JsonSchemaResponseValidator.cs',
        'src/SmartHopper.Infrastructure/AICall/Validation/ToolCapabilityValidator.cs',
        'src/SmartHopper.Infrastructure/AICall/Validation/ToolExistsValidator.cs',
        'src/SmartHopper.Infrastructure/AICall/Validation/ToolJsonSchemaValidator.cs',
        'src/SmartHopper.Infrastructure/AIModels/ModelManager.cs',
        'src/SmartHopper.Infrastructure/AIProviders/AIProvider.cs',
        'src/SmartHopper.Infrastructure/AIProviders/AIProviderSettings.cs',
        'src/SmartHopper.Infrastructure/AIProviders/AIProviderStreamingAdapter.cs',
        'src/SmartHopper.Infrastructure/AIProviders/ProviderManager.cs',
        'src/SmartHopper.Infrastructure/AITools/ToolManager.cs',
        'src/SmartHopper.Infrastructure/Dialogs/StyledMessageDialog.cs',
        'src/SmartHopper.Infrastructure/Settings/SmartHopperSettings.cs',
        'src/SmartHopper.Core/ComponentBase/StatefulAsyncComponentBase.cs',
        'src/SmartHopper.Core/ComponentBase/StateManager.cs',
        'src/SmartHopper.Core/DataTree/DataTreeProcessor.cs',
        'src/SmartHopper.Core/IO/GHPersistenceService.cs',
        'src/SmartHopper.Core/IO/SafeStructureCodec.cs',
        'src/SmartHopper.Core/UI/CanvasButton.cs',
        'src/SmartHopper.Core/UI/Chat/WebChatDialog.cs',
        'src/SmartHopper.Core/UI/Chat/WebChatObserver.cs',
        'src/SmartHopper.Core/UI/Chat/WebChatUtils.cs',
        'src/SmartHopper.Menu/Dialogs/AboutDialog.cs',
        'src/SmartHopper.Menu/Dialogs/SettingsDialog.cs',
        'src/SmartHopper.Menu/Dialogs/SettingsTabs/GenericProviderSettingsPage.cs',
        'src/SmartHopper.Menu/SmartHopperMenu.cs',
        'src/SmartHopper.Core.Grasshopper/AITools/gh_move.cs',
        'src/SmartHopper.Core.Grasshopper/AITools/gh_tidy_up.cs',
        'src/SmartHopper.Core.Grasshopper/AITools/script_new.cs',
        'src/SmartHopper.Core.Grasshopper/AITools/script_review.cs',
        'src/SmartHopper.Core.Grasshopper/AITools/web_generic_page_read.cs',
        'src/SmartHopper.Core.Grasshopper/AITools/web_rhino_forum_read_post.cs',
        'src/SmartHopper.Core.Grasshopper/AITools/web_rhino_forum_search.cs',
        'src/SmartHopper.Core.Grasshopper/Converters/NumberSliderUtils.cs',
        'src/SmartHopper.Core.Grasshopper/Graph/DependencyGraphUtils.cs',
        'src/SmartHopper.Core.Grasshopper/Utils/GHCanvasUtils.cs',
        'src/SmartHopper.Core.Grasshopper/Utils/GHComponentUtils.cs',
        'src/SmartHopper.Core.Grasshopper/Utils/GHDocumentUtils.cs',
        'src/SmartHopper.Core.Grasshopper/Utils/GHPropertyManager.cs',
        'src/SmartHopper.Core.Grasshopper/Utils/ParsingTools.cs'
    ) | ForEach-Object { Join-Path -Path $PSScriptRoot -ChildPath $_ }
}

$fixed = 0
foreach ($f in $Files) {
    if (Test-Path -LiteralPath $f) {
        ProcessFile -filePath $f
        $fixed++
    } else {
        Write-Host "File not found, skipping: $f"
    }
}

Write-Host "Processed $fixed file(s)."