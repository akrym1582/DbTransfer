# Documentation

## Command reference

The command pages contain generated `--help` output and usage examples:

- [`copy`](commands/copy.md)
- [`export`](commands/export.md)
- [`import`](commands/import.md)
- [`exec`](commands/exec.md)
- [`inspect`](commands/inspect.md)
- [`validate`](commands/validate.md)

The examples describe the implemented CLI. Generate all command pages from the actual executable with:

```powershell
pwsh ./scripts/Generate-CommandDocs.ps1
```

Files in `docs/commands` are generated. Change CLI attributes or the examples in the generator instead of editing a generated page directly.
