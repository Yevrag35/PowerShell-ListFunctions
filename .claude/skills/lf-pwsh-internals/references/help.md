# Help files

Measured 2026-10-10 on PowerShell 7.6.6 and Windows PowerShell 5.1.26100.9444, with copies of the Debug build that hold a test MAML file, and with the module's own help file.

## Where Get-Help looks

- **The file name:** a binary cmdlet's `HelpFile` is its assembly's file name followed by `-Help.xml`: `ListFunctions.Next.dll-Help.xml` in PowerShell 7, and `ListFunctions.NETFramework.dll-Help.xml` in Windows PowerShell 5.1.
- **Imported by the DLL's path,** as the tests and `Debug.ps1` import it, the cmdlets' module is the DLL, and Get-Help finds the file in a culture folder of the DLL's folder, such as `en-US`, or in the DLL's folder itself.
- **Imported through `ListFunctions.psd1`,** whose `.psm1` imports the DLL from `Core` or `Desk`, the cmdlets' module is ListFunctions, whose module base is the manifest's folder. Get-Help finds the file in `ListFunctions/en-US`, and in `Core/en-US` or `Desk/en-US`. When both hold it, the one in `ListFunctions/en-US` wins.
- **Culture:** a folder for the UI culture wins, such as `de-DE` under de-DE. Without one, both editions fall back to `en-US`, under de-DE and under fr-FR.
- **An installed copy:** with auto-loading on and ListFunctions 3.1.0 installed, `Get-Help New-List` still returns only the loaded build's help. A module-qualified name, such as `Get-Help ListFunctions.Next\New-List`, works in both editions.

## What Get-Help shows

- **The syntax and the parameter attributes come from the file as it's written.** A syntax block that names a parameter the cmdlet doesn't have shows it, and PARAMETERS lists only the parameters that the file describes, with the file's `required`, `position`, `pipelineInput`, and `aliases`. Get-Help adds `[<CommonParameters>]` to each syntax line, and a `<CommonParameters>` entry after the parameters, by itself.
- **A parameter's aliases:** PowerShell 7 shows an `Aliases` row for each parameter. Windows PowerShell 5.1 doesn't show them.
- **A command's aliases:** with a help file, Get-Help has no ALIASES section in either edition.
- **Without a help file,** Get-Help makes up help from the cmdlet. Its type is `CmdletHelpInfo`, its synopsis is the syntax, and its description is empty. Help from a file has the type `MamlCommandHelpInfo#<module>#<cmdlet>`, such as `MamlCommandHelpInfo#ListFunctions.Next#New-List`.
- **Paragraphs:** Get-Help wraps each `maml:para` at the console's width, with a blank line between paragraphs. A paragraph that starts with `- ` prints as it is, so it reads as a list item. An XML comment before `helpItems` doesn't get in the way.
- **Examples:** each example's title, code, and remarks print as they're written, so a long title wraps too. When an example has no remarks, Get-Help indents the next example's title by a space. When the remarks don't end with an empty `maml:para`, the next title follows them without a blank line.

## The help object

- In `Get-Help -Full` output, the entries of `parameters.parameter` and of `syntax.syntaxItem.parameter` hold `name`, `required`, `position`, `pipelineInput`, and `aliases` as the strings that the file writes, in both editions. A parameter's type is `type.name`. In a syntax block, `parameterValue` is the text of the `command:parameterValue` element, or `$null` when the parameter has none.
- `[System.Management.Automation.Cmdlet]::CommonParameters` and `OptionalCommonParameters` list the common parameters in both editions. PowerShell 7's list includes `ProgressAction`.

## Update-Help

- With a `HelpInfoURI` that isn't a folder of updatable help, such as a GitHub issues page, `Update-Help -Module ListFunctions` fails in both editions: "The HelpInfoUri '...' does not resolve to a container."
- Without a `HelpInfoURI`, `Update-Help -Module ListFunctions` fails, and so does `-Module ListFunctions*`: "The Update-Help command failed because the specified module does not support updatable help."

## In ListFunctions

- The help file is `src/engine/ListFunctions-Next/en-US/ListFunctions.Next.dll-Help.xml`. Both projects copy it into an `en-US` folder in their output, the NETFramework project as `ListFunctions.NETFramework.dll-Help.xml`. The tests, which import the DLL by its path, get the help that way, and the shipped module gets it in `Core/en-US` and `Desk/en-US` from the Release outputs.
- Because Get-Help trusts the file, `tests/Module.Tests.ps1` compares each cmdlet's syntax blocks and parameters with the cmdlet's parameter sets and parameters. It reads the help with a module-qualified name, and checks for the `MamlCommandHelpInfo` type, so it also fails when the build doesn't copy the file.
- The help lists each cmdlet's aliases in its notes, because Get-Help shows no ALIASES section.
- Every example in the file has remarks that end with an empty paragraph, because of the spacing above.
- The manifest has no `HelpInfoURI`, because the module ships its help and has no updatable help.
