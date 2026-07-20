#!/usr/bin/env bash

set -euo pipefail

repository_root="$(git rev-parse --show-toplevel)"
link_pattern='\]\(([^)]*)\)'
failed=0

while IFS= read -r -d '' markdown_file; do
    markdown_directory="$(dirname "$markdown_file")"

    while IFS= read -r line || [[ -n "$line" ]]; do
        remaining="$line"

        while [[ "$remaining" =~ $link_pattern ]]; do
            match="${BASH_REMATCH[0]}"
            target="${BASH_REMATCH[1]}"
            remaining="${remaining#*"$match"}"

            target="${target%%#*}"
            target="${target#<}"
            target="${target%>}"

            if [[ -z "$target" || "$target" == http://* || "$target" == https://* || "$target" == mailto:* ]]; then
                continue
            fi

            if [[ ! -e "$markdown_directory/$target" ]]; then
                printf 'Broken Markdown link in %s: %s\n' "${markdown_file#"$repository_root"/}" "$target" >&2
                failed=1
            fi
        done
    done < "$markdown_file"
done < <(find "$repository_root" -type f -name '*.md' -print0)

exit "$failed"
