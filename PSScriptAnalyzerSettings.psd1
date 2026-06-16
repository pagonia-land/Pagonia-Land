@{
    # PSScriptAnalyzer config for this repo's scripts/ tooling.
    # Auto-discovered by the VSCode PowerShell extension (workspace root) and
    # honoured by `Invoke-ScriptAnalyzer -Settings ./PSScriptAnalyzerSettings.psd1`.
    #
    # We surface every Warning/Error rule EXCEPT the ones below, which fire on
    # deliberate choices rather than real defects:
    #
    #   PSAvoidUsingWriteHost
    #     These scripts are interactive CLI tools whose job is console output;
    #     Write-Host is the intended sink, not accidental logging.
    #
    #   PSUseSingularNouns
    #     Cosmetic verb-noun naming guidance; not worth renaming helper functions.
    #
    #   PSUseShouldProcessForStateChangingFunctions
    #     Our state-changing helpers are plain script functions, not cmdlets that
    #     need -WhatIf/-Confirm plumbing.
    #
    #   PSUseBOMForUnicodeEncodedFile
    #     Files are UTF-8 (no BOM) by design; a BOM would break tooling that reads
    #     them as plain ASCII/UTF-8.
    Severity     = @('Error', 'Warning')
    ExcludeRules = @(
        'PSAvoidUsingWriteHost',
        'PSUseSingularNouns',
        'PSUseShouldProcessForStateChangingFunctions',
        'PSUseBOMForUnicodeEncodedFile'
    )
}
