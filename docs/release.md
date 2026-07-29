# Release Process

Use this workflow for the fastest safe release.

## One-Pass Checklist

1. Update versions in `Chronicler.csproj`, `Chronicler.json`, and `repo.json`.
2. Update `repo.json` download links to the new tag URL.
3. Update `repo.json` `LastUpdated` with the current Unix timestamp.
4. Build locally:

```powershell
dotnet build -c Release -o output\
```

5. Inspect changes:

```powershell
git status --short --branch
git diff --stat
git diff
```

6. Commit and push `main`:

```powershell
git add Chronicler.csproj Chronicler.json repo.json docs/release.md
git add Configuration Features Plugin UI
git commit -m "Release vX.Y.Z.W"
git push origin main
```

7. Create and push an annotated tag:

```powershell
git tag -a vX.Y.Z.W -m "vX.Y.Z.W"
git push origin vX.Y.Z.W
```

8. Wait for the release workflow:

```powershell
gh run list --workflow "Create Release" --limit 5
gh run watch <run-id> --exit-status
```

9. Confirm the release URL:

```powershell
gh release view vX.Y.Z.W --json url,tagName,name
```

## Notes

- Tags must use the `vX.Y.Z.W` format because `repo.json` download links use that tag.
- The workflow builds the zip on GitHub Actions; do not upload local `output` artifacts manually.
- If tag creation opens an editor, cancel and use `git tag -a vX.Y.Z.W -m "vX.Y.Z.W"`.
