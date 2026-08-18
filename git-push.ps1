# Push to dhhieu113pro/roslyn-mcp
# Run: .\push.ps1

Remove-Item Env:\GH_TOKEN -ErrorAction SilentlyContinue
gh auth switch -u dhhieu113pro
git push -u origin main
