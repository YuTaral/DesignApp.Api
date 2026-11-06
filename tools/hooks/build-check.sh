#!/usr/bin/env bash
# Claude Code Stop hook: when a turn ends with uncommitted C# changes, build the solution and, if it
# fails, hand the errors back (exit 2) so the turn continues and they get fixed.
# - Quiet and instant when no .cs/.csproj file changed, or when the same changes already built fine.
# - Builds into a temp folder: the Visual Studio F5 run keeps the normal bin folders locked.
set -u
input=$(cat)
# A turn the hook already sent back: don't loop; the next turn checks again.
case "$input" in *'"stop_hook_active":true'*|*'"stop_hook_active": true'*) exit 0 ;; esac

repo="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$repo" || exit 0
changes=$(git status --porcelain -- '*.cs' '*.csproj' '*.props' 2>/dev/null)
[ -z "$changes" ] && exit 0

tmp="${TEMP:-${TMPDIR:-/tmp}}"
stamp="$tmp/designapp-build-check.ok"
fingerprint=$( { echo "$changes"; git diff -- '*.cs' '*.csproj' '*.props'; git ls-files --others --exclude-standard -- '*.cs' | xargs -r cat; } | sha1sum | cut -d' ' -f1)
[ -f "$stamp" ] && [ "$(cat "$stamp")" = "$fingerprint" ] && exit 0

out=$(dotnet build DesignApp.slnx --artifacts-path "$tmp/designapp-build-check" -nologo -v q 2>&1)
if [ $? -eq 0 ]; then
  echo "$fingerprint" > "$stamp"
  exit 0
fi
{
  echo "dotnet build failed after this turn's C# changes. Fix these before finishing:"
  echo "$out" | grep -E ': (error|warning) ' | sed -E 's/ \[[^]]*\]$//' | sort -u | head -25
} >&2
exit 2
