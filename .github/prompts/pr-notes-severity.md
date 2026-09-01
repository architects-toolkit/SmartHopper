# PR change severity prompt

You are reviewing an update to a pull request.

Previous suggested title:
{{PREVIOUS_TITLE}}

Previous suggested description:
{{PREVIOUS_BODY}}

Here is the diff of the new changes:
{{RAW_DIFF}}

Based only on this diff, are the changes substantial enough that the PR title and/or description should be updated, or are they only cosmetic, code style, or minor improvements that do not affect end users?

Answer with exactly one word: `yes` if the title/description should be updated, or `no` if the changes are too minor to require an update.
